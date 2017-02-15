using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Obvs.Configuration;
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
        IObservable<T> GetResponse<T>(TRequest request) where T : TResponse;

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

        public IObservable<T> GetResponse<T>(IRequest request) where T : IResponse
        {
            return _serviceBusClient.GetResponse<T>(request);
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
        protected readonly IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> Endpoints;
        private readonly IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> _endpointClients;
        private readonly IObservable<TEvent> _events;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;
        private readonly List<KeyValuePair<object, IObservable<TMessage>>> _subscribers;
        private readonly IMessageBus<TMessage> _localBus;
        private readonly LocalBusOptions _localBusOption;

        public ServiceBusClient(IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> endpointClients, 
                                IEnumerable<IServiceEndpoint<TMessage, TCommand, TEvent, TRequest, TResponse>> endpoints, 
                                IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider, 
                                IMessageBus<TMessage> localBus = null, LocalBusOptions localBusOption = LocalBusOptions.MessagesWithNoEndpointClients)
        {
            _localBus = localBus;
            _localBusOption = localBusOption;

            Endpoints = endpoints.ToList();

            _endpointClients = endpointClients.ToArray();

            _events = _endpointClients
                .Select(endpointClient => endpointClient.EventsWithErrorHandling(_exceptions))
                .Merge()
                .Merge(GetLocalMessages<TEvent>())
                .PublishRefCountRetriable();

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

            var tasks = EndpointClientsThatCanHandle(command)
                .Select(endpoint => Catch(() => endpoint.SendAsync(command), exceptions, CommandErrorMessage(endpoint)))
                .Union(PublishLocal(command, exceptions))
                .ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(command), exceptions);
            }

            if (tasks.Length == 0)
            {
                throw new Exception(string.Format("No endpoint or local bus configured for {0}, please check your ServiceBus configuration.", command));
            }

            return Task.WhenAll(tasks);
        }

        protected IObservable<T> GetLocalMessages<T>()
        {
            return _localBus == null ? Observable.Empty<T>() : _localBus.Messages.OfType<T>();
        }

        protected IEnumerable<Task> PublishLocal(TMessage message, List<Exception> exceptions)
        {
            return ShouldPublishLocally(message) ? new[] {Catch(() => _localBus.PublishAsync(message), exceptions)} : 
                                                   Enumerable.Empty<Task>();
        }

        private bool ShouldPublishLocally(TMessage message)
        {
            if (_localBus == null)
            {
                return false;
            }

            if (_localBusOption == LocalBusOptions.MessagesWithNoEndpointClients &&
                !_endpointClients.Any(e => e.CanHandle(message)))
            {
                return true;
            }

            if (_localBusOption == LocalBusOptions.MessagesWithNoEndpoints && 
                !Endpoints.Any(e => e.CanHandle(message)) &&
                !_endpointClients.Any(e => e.CanHandle(message)))
            {
                return true;
            }

            return false;
        }

        public Task SendAsync(IEnumerable<TCommand> commands)
        {
            var exceptions = new List<Exception>();
            
            var tasks = commands.ToArray().Select(command => Catch(() => SendAsync(command), exceptions)).ToArray();

            if (exceptions.Any())
            {
                throw new AggregateException(CommandErrorMessage(), exceptions.Cast<AggregateException>().SelectMany(e => e.InnerExceptions));
            }

            if (tasks.Length == 0)
            {
                throw new Exception("No endpoint or local bus configured for any of these commands, please check your ServiceBus configuration.");
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

            return EndpointClientsThatCanHandle(request)
                .Select(endpoint => endpoint.GetResponses(request)
                .Where(response => _requestCorrelationProvider.AreCorrelated(request, response)))
                .Merge()
                .Merge(GetLocalResponses(request))
                .PublishRefCountRetriable();
        }

        private IObservable<TResponse> GetLocalResponses(TRequest request)
        {
            if (ShouldPublishLocally(request))
            {
                return Observable.Create<TResponse>(observer =>
                {
                    IDisposable disposable = _localBus.Messages.OfType<TResponse>()
                        .Where(response => _requestCorrelationProvider.AreCorrelated(request, response))
                        .Subscribe(observer);

                    _localBus.PublishAsync(request);

                    return disposable;
                });
            }
            return Observable.Empty<TResponse>();
        }

        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>();
        }

        public IObservable<T> GetResponse<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>().Take(1);
        }

        public virtual IDisposable Subscribe(object subscriber, IScheduler scheduler = null)
        {
            return Subscribe(subscriber, Events, scheduler);
        }

        protected IDisposable Subscribe(object subscriber, IObservable<TMessage> messages, IScheduler scheduler = null, IObservable<TRequest> requests = null, Action<TRequest, TResponse> onReply = null)
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

                Action<object, TMessage> onMessage = null;
                Func<object, TRequest, IObservable<TResponse>> onRequest = null;

                if (returnType == typeof(void))
                {
                    onMessage = CreateSubscriberAction<TMessage>(subscriberType, methodInfo);
                }
                else if (onReply != null)
                {
                    onRequest = CreateSubscriberFunc<TRequest, IObservable<TResponse>>(subscriberType, methodInfo);
                }

                subscription.Add(observable
                    .Where(message => paramType.IsInstanceOfType(message))
                    .Subscribe(message =>
                    {
                        try
                        {
                            if (onMessage != null)
                            {
                                onMessage(subscriber, message);
                            }
                            else if (onRequest != null && onReply != null)
                            {
                                var request = message as TRequest;
                                onRequest(subscriber, request).Subscribe(response => onReply(request, response));
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
                throw new ArgumentException("Subscriber needs at least one public method of format 'void OnMessage(TMessage msg)' or 'IObservable<TResponse> OnRequest(TRequest req)'", "subscriber");
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

        private static Action<object, TParamType> CreateSubscriberAction<TParamType>(Type subscriberType, MethodInfo methodInfo)
        {
            var name = subscriberType.Name + methodInfo.Name + "DynamicMethod";
            var shim = new DynamicMethod(name, typeof(void), new[] { typeof(object), typeof(TParamType) }, subscriberType);
            var il = shim.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // Load subscriber
            il.Emit(OpCodes.Ldarg_1); // Load parameter
            il.Emit(OpCodes.Call, methodInfo); // Invoke method
            il.Emit(OpCodes.Ret); // void return

            return (Action<object, TParamType>)shim.CreateDelegate(typeof(Action<object, TParamType>));
        }

        private static Func<object, TParamType, TReturnType> CreateSubscriberFunc<TParamType, TReturnType>(Type subscriberType, MethodInfo methodInfo)
        {
            var name = subscriberType.Name + methodInfo.Name + "DynamicMethod";
            var shim = new DynamicMethod(name, typeof(TReturnType), new[] { typeof(object), typeof(TParamType) }, subscriberType);
            var il = shim.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // Load subscriber
            il.Emit(OpCodes.Ldarg_1); // Load parameter
            il.Emit(OpCodes.Call, methodInfo); // Invoke method
            il.Emit(OpCodes.Ret); // return

            return (Func<object, TParamType, TReturnType>)shim.CreateDelegate(typeof(Func<object, TParamType, TReturnType>));
        }

        private IEnumerable<IServiceEndpointClient<TMessage, TCommand, TEvent, TRequest, TResponse>> EndpointClientsThatCanHandle(TMessage message)
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