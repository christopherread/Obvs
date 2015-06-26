using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public interface IServiceBusClient : IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
    }

    public interface IServiceBusClient<TMessage, in TCommand, out TEvent, in TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        IObservable<TEvent> Events { get; }

        Task SendAsync(TCommand command);
        Task SendAsync(IEnumerable<TCommand> commands);

        IObservable<TResponse> GetResponses(TRequest request);
        IObservable<T> GetResponses<T>(TRequest request) where T : TResponse;

        IObservable<Exception> Exceptions { get; }

        IDisposable Subscribe(object subscriber, IScheduler scheduler = null);
    }

    public class ServiceBusClient : IServiceBusClient, IDisposable
    {
        private readonly IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> _serviceBusClient;

        public ServiceBusClient(IServiceBusClient<IMessage, ICommand, IEvent, IRequest, IResponse> serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public IObservable<IEvent> Events
        {
            get { return _serviceBusClient.Events; }
        }

        public Task SendAsync(ICommand command)
        {
            return _serviceBusClient.SendAsync(command);
        }

        public Task SendAsync(IEnumerable<ICommand> commands)
        {
            return _serviceBusClient.SendAsync(commands);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _serviceBusClient.GetResponses(request);
        }

        public IObservable<T> GetResponses<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponses<T>(request);
        }

        public IObservable<Exception> Exceptions
        {
            get { return _serviceBusClient.Exceptions; }
        }

        public IDisposable Subscribe(object subscriber, IScheduler scheduler = null)
        {
            return _serviceBusClient.Subscribe(subscriber, scheduler);
        }

        public void Dispose()
        {
            ((IDisposable)_serviceBusClient).Dispose();
        }
    }

    public class ServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse> : ServiceBusErrorHandlingBase<TMessage, TCommand, TEvent, TRequest, TResponse>, IServiceBusClient<TMessage, TCommand, TEvent, TRequest, TResponse>
        where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients;
        private readonly IObservable<TEvent> _events;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;
        private readonly List<KeyValuePair<object, IObservable<TMessage>>> _subscribers;

        public ServiceBusClient(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            _endpointClients = endpointClients.ToArray();
            _events = _endpointClients.Select(EventsWithErroHandling).Merge().Publish().RefCount();
            _requestCorrelationProvider = requestCorrelationProvider;
            _subscribers = new List<KeyValuePair<object, IObservable<TMessage>>>();
        }

        public IObservable<TEvent> Events
        {
            get { return _events; }
        }

        public Task SendAsync(TCommand command)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = EndpointsThatCanHandle(command).Select(endpoint => Catch(() => endpoint.SendAsync(command), exceptions, CommandErrorMessage(endpoint))).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            return Task.WhenAll(tasks);
        }

        public Task SendAsync(IEnumerable<TCommand> commands)
        {
            List<Exception> exceptions = new List<Exception>();

            var tasks = commands.ToArray().Select(command => Catch(() => SendAsync(command), exceptions)).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(), exceptions.Cast<AggregateException>().SelectMany(e => e.InnerExceptions));
            }

            return Task.WhenAll(tasks);
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the ServiceBus with a IRequestCorrelationProvider using the fluent configuration extension method .CorrelatesRequestWith()");
            }

            _requestCorrelationProvider.SetRequestCorrelationIds(request);

            return EndpointsThatCanHandle(request).Select(endpoint => endpoint.GetResponses(request)
                                                  .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                                                  .Merge().Publish().RefCount();
        }

        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            IObservable<TResponse> observable = GetResponses(request);
            return observable.OfType<T>();
        }

        public virtual IDisposable Subscribe(object subscriber, IScheduler scheduler = null)
        {
            return Subscribe(subscriber, Events, scheduler);
        }

        protected IDisposable Subscribe(object subscriber, IObservable<TMessage> messages, IScheduler scheduler = null, IObservable<TMessage> requests = null, Action<TRequest, TResponse> reply = null)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }

            IObservable<TMessage> observable;
            scheduler = scheduler ?? Scheduler.Default;

            lock (_subscribers)
            {
                if (_subscribers.Any(sub => sub.Key == subscriber))
                {
                    throw new ArgumentException("Already subscribed", "subscriber");
                }
                observable = messages;
                if (requests != null)
                {
                    observable = observable.Merge(requests);
                }
                observable = observable.ObserveOn(scheduler);
                _subscribers.Add(new KeyValuePair<object, IObservable<TMessage>>(subscriber, observable));
            }

            var subscriberType = subscriber.GetType();
            var methodHandlers = subscriberType.GetSubscriberMethods<TCommand, TEvent, TRequest, TResponse>();
            
            var subscription = new CompositeDisposable();
            foreach (var methodHandler in methodHandlers)
            {
                var methodInfo = methodHandler.Item1;
                var paramType = methodHandler.Item2;
                var returnType = methodHandler.Item3;
                var isVoidReturn = returnType == typeof (void);
                bool hasReplyAction = reply != null;

                Action<object, TMessage> onMessage = null;
                Func<object, TRequest, IObservable<TResponse>> onRequest = null;

                if (isVoidReturn)
                {
                    onMessage = CreateSubscriberAction(subscriberType, methodInfo);
                }
                else if (hasReplyAction)
                {
                    onRequest = CreateSubscriberFunc(subscriberType, methodInfo);
                }

                subscription.Add(observable
                    .Where(message => paramType.IsInstanceOfType(message))
                    .Subscribe(message =>
                    {
                        try
                        {
                            if (isVoidReturn)
                            {
                                onMessage(subscriber, message);
                            }
                            else if (hasReplyAction)
                            {
                                var request = message as TRequest;
                                onRequest(subscriber, request).Subscribe(response => reply(request, response));
                            }
                        }
                        catch (Exception exception)
                        {
                            _exceptions.OnNext(exception);
                        }
                    }));
            }

            if (!subscription.Any())
            {
                throw new ArgumentException("Subscriber needs at least one public method of format 'void MethodName(TMessage msg)'", "subscriber");
            }

            subscription.Add(Disposable.Create(() =>
            {
                lock (_subscribers)
                {
                    _subscribers.RemoveAll(sub => sub.Key == subscriber);
                }
            }));

            return subscription;
        }

        private Action<object, TMessage> CreateSubscriberAction(Type subscriberType, MethodInfo methodInfo)
        {
            DynamicMethod shim = new DynamicMethod(subscriberType.Name + methodInfo.Name, typeof(void), new[] { typeof(object), typeof(TMessage) }, GetType());
            ILGenerator il = shim.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // Load subscriber
            il.Emit(OpCodes.Ldarg_1); // Load parameter
            il.Emit(OpCodes.Call, methodInfo); // Invoke method
            il.Emit(OpCodes.Ret); // void return

            return (Action<object, TMessage>)shim.CreateDelegate(typeof(Action<object, TMessage>));
        }

        private Func<object, TRequest, IObservable<TResponse>> CreateSubscriberFunc(Type subscriberType, MethodInfo methodInfo)
        {
            DynamicMethod shim = new DynamicMethod(subscriberType.Name + methodInfo.Name, typeof(IObservable<TResponse>), new[] { typeof(object), typeof(TRequest) }, GetType());
            ILGenerator il = shim.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // Load subscriber
            il.Emit(OpCodes.Ldarg_1); // Load parameter
            il.Emit(OpCodes.Call, methodInfo); // Invoke method
            il.Emit(OpCodes.Ret); // return

            return (Func<object, TRequest, IObservable<TResponse>>)shim.CreateDelegate(typeof(Func<object, TRequest, IObservable<TResponse>>));
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointsThatCanHandle(TMessage message)
        {
            return _endpointClients.Where(endpoint => endpoint.CanHandle(message)).ToArray();
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse> endpointClient in _endpointClients)
            {
                endpointClient.Dispose();
            }
        }
    }
}