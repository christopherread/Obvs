using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Apache.NMS;
using Obvs.ActiveMQ.Utils;
using Obvs.ActiveMQ.Extensions;
using Obvs.Hosting;
using Obvs.Transports;
using Obvs.Serialization;

namespace Obvs.ActiveMQ
{
    /// <summary>
    /// Sends messages to an ActiveMQ queue or topic as bytes messages, as v6 did, for an app that builds its own endpoints.
    /// Each carries its type's name and the properties from <c>propertyProvider</c> as message properties, and its headers
    /// as string properties. Messages are sent one at a time, in order, on the scheduler it's given or one of its own.
    /// </summary>
    /// <typeparam name="TMessage">The type of the messages, such as the service's commands or events</typeparam>
    public class MessagePublisher<TMessage> : IMessagePublisher<TMessage>
        where TMessage : class
    {
        private const int DrainTimeoutSecs = 2;

        private readonly Lazy<IConnection> _connection;
        private readonly IDestination _destination;
        private readonly IMessageSerializer _serializer;
        private readonly Func<TMessage, Dictionary<string, object>> _propertyProvider;
        private readonly Func<TMessage, MsgDeliveryMode> _deliveryMode;
        private readonly Func<TMessage, MsgPriority> _priority;
        private readonly Func<TMessage, TimeSpan> _timeToLive;
        private readonly Func<TMessage, IReadOnlyDictionary<string, string>, CancellationToken, Task> _publishMethod;
        private readonly Func<Task> _drainMethod;
        private readonly object _gate = new object();

        private ISession _session;
        private IMessageProducer _producer;
        private volatile IDisposable _disposable;
        private volatile bool _disposed;

        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages one at a time, in order, on a scheduler of its own.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        protected MessagePublisher(
            Lazy<IConnection> lazyConnection,
            IDestination destination,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, object>> propertyProvider,
            Func<TMessage, MsgDeliveryMode> deliveryMode = null,
            Func<TMessage, MsgPriority> priority = null,
            Func<TMessage, TimeSpan> timeToLive = null)
        {
            _connection = lazyConnection;
            _destination = destination;
            _serializer = serializer;
            _propertyProvider = propertyProvider ?? (message => new Dictionary<string, object>()) ;
            _deliveryMode = deliveryMode ?? (message => MsgDeliveryMode.NonPersistent);
            _priority = priority ?? (message => MsgPriority.Normal);
            _timeToLive = timeToLive ?? (message => TimeSpan.Zero);

            var taskFactory = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
            _publishMethod = (message, headers, cancellationToken) => taskFactory.StartNew(() => Publish(message, headers, cancellationToken), cancellationToken);
            _drainMethod = () => taskFactory.StartNew(() => { });
        }

        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages on <paramref name="scheduler"/>.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="scheduler">Where messages are sent, which must run one at a time</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        public MessagePublisher(Lazy<IConnection> lazyConnection,
            IDestination destination,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, object>> propertyProvider,
            IScheduler scheduler,
            Func<TMessage, MsgDeliveryMode> deliveryMode = null,
            Func<TMessage, MsgPriority> priority = null,
            Func<TMessage, TimeSpan> timeToLive = null)
            : this(lazyConnection, destination, serializer, propertyProvider, deliveryMode, priority, timeToLive)
        {
            _publishMethod = (message, headers, cancellationToken) => scheduler.ScheduleAsync(() => Publish(message, headers, cancellationToken));
            _drainMethod = () => scheduler.ScheduleAsync(() => { });
        }

        /// <summary>Creates a publisher to <paramref name="destination"/> that sends messages on <paramref name="taskScheduler"/>.</summary>
        /// <param name="lazyConnection">The connection to ActiveMQ, opened on the first message</param>
        /// <param name="destination">The queue or topic</param>
        /// <param name="serializer">Writes each message's body</param>
        /// <param name="propertyProvider">The properties to send with each message, which sources can filter or select on. None when null</param>
        /// <param name="taskScheduler">Where messages are sent, which must run one at a time, for example a <see cref="ConcurrentExclusiveSchedulerPair"/>'s exclusive scheduler</param>
        /// <param name="deliveryMode">Whether each message is persistent. Non-persistent when null</param>
        /// <param name="priority">Each message's priority. Normal when null</param>
        /// <param name="timeToLive">How long each message lives. For ever when null</param>
        public MessagePublisher(
            Lazy<IConnection> lazyConnection,
            IDestination destination,
            IMessageSerializer serializer,
            Func<TMessage, Dictionary<string, object>> propertyProvider,
            TaskScheduler taskScheduler,
            Func<TMessage, MsgDeliveryMode> deliveryMode = null,
            Func<TMessage, MsgPriority> priority = null,
            Func<TMessage, TimeSpan> timeToLive = null)
            : this(lazyConnection, destination, serializer, propertyProvider, deliveryMode, priority, timeToLive)
        {
            var taskFactory = new TaskFactory(taskScheduler);
            _publishMethod = (message, headers, cancellationToken) => taskFactory.StartNew(() => Publish(message, headers, cancellationToken), cancellationToken);
            _drainMethod = () => taskFactory.StartNew(() => { });
        }

        /// <inheritdoc />
        public Task PublishAsync(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("MessagePublisher has been disposed already.");
            }

            return _publishMethod(message, headers, cancellationToken);
        }

        // A publish still waiting its turn is dropped if it's cancelled by then
        private void Publish(TMessage message, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Publish(message, _propertyProvider(message) ?? new Dictionary<string, object>(), headers);
        }

        private void Publish(TMessage message, Dictionary<string, object> properties, IReadOnlyDictionary<string, string> headers)
        {
            if (_disposed)
            {
                return;
            }

            Connect();

            AppendTypeNameProperty(message, properties);

            var msg = GenerateMessage(message, _producer, _serializer);

            msg.Properties.AddProperties(properties);
            SetHeaders(msg, headers);

            _producer.Send(msg, _deliveryMode(message), _priority(message), _timeToLive(message));
        }
        
        /// <summary>Creates the ActiveMQ message for <paramref name="message"/>, a bytes message unless overridden.</summary>
        /// <param name="message">The message to send</param>
        /// <param name="producer">Creates the ActiveMQ message</param>
        /// <param name="serializer">Writes the message's body</param>
        /// <returns>The message to send, whose properties the publisher then sets</returns>
        protected virtual IMessage GenerateMessage(TMessage message, IMessageProducer producer, IMessageSerializer serializer)
        {
            var bytesMessage = producer.CreateBytesMessage();

            using (var stream = new MemoryStream())
            {
                var offset = stream.Position;

                serializer.Serialize(stream, message);
                
                bytesMessage.WriteBytes(stream.GetBuffer(), (int)offset, (int)(stream.Position - offset));
            }

            return bytesMessage;
        }

        // The Obvs headers (such as trace context) as message properties, which v6 receivers ignore.
        // The type name property Obvs relies on is never overwritten.
        private static void SetHeaders(IMessage msg, IReadOnlyDictionary<string, string> headers)
        {
            foreach (var header in headers)
            {
                if (header.Key != MessagePropertyNames.TypeName)
                {
                    msg.Properties.SetString(header.Key, header.Value);
                }
            }
        }

        private static void AppendTypeNameProperty(TMessage message, Dictionary<string, object> properties)
        {
            try
            {
                properties.Add(MessagePropertyNames.TypeName, message.GetType().Name);
            }
            catch (ArgumentException exception)
            {
                throw new Exception(string.Format("Failed to add '{0}' property to message property dictionary. Please ensure the property dictionary provided has not been used before, and doesn't contain a property called '{0}' already.", MessagePropertyNames.TypeName), exception);
            }
        }

        private void Connect()
        {
            if (_disposable == null)
            {
                lock (_gate)
                {
                    if (_disposable == null)
                    {
                        _session = _connection.Value.CreateSession(Apache.NMS.AcknowledgementMode.AutoAcknowledge);
                        _producer = _session.CreateProducer(_destination);

                        _disposable = Disposable.Create(() =>
                        {
                            _disposed = true;

                            DrainScheduler();

                            ConnectionClosing.Close(_connection.Value, () =>
                            {
                                _producer.Close();
                                _producer.Dispose();
                                _session.Close();
                                _session.Dispose();
                            });
                        });
                    }
                }
            }
        }

        private void DrainScheduler()
        {
            // Await end of queue (if _taskFactory is a queue/orderedtaskscheduler)
            _drainMethod().Wait(TimeSpan.FromSeconds(DrainTimeoutSecs));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var disposable = _disposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}