using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Extensions;
using Obvs.Types;

namespace Obvs
{
    public class SubjectServiceBus : SubjectServiceBus<IMessage, ICommand, IEvent, IRequest, IResponse>, IServiceBus
    {
        public SubjectServiceBus(IRequestCorrelationProvider requestCorrelationProvider = null) 
            : base(requestCorrelationProvider ?? new DefaultRequestCorrelationProvider())
        {
        }

        public SubjectServiceBus(IScheduler scheduler, IRequestCorrelationProvider requestCorrelationProvider) 
            : base(scheduler, requestCorrelationProvider)
        {
        }
    }

    public class SubjectServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse> : IServiceBus<TMessage, TCommand, TEvent, TRequest, TResponse>
       where TMessage : class
        where TCommand : class, TMessage
        where TEvent : class, TMessage
        where TRequest : class, TMessage
        where TResponse : class, TMessage
    {
        private readonly ISubject<TCommand, TCommand> _commands;
        private readonly ISubject<TEvent, TEvent> _events;
        private readonly ISubject<TRequest, TRequest> _requests;
        private readonly ISubject<TResponse, TResponse> _responses;
        private readonly IRequestCorrelationProvider<TRequest, TResponse> _requestCorrelationProvider;

        public SubjectServiceBus(IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
            : this(null, requestCorrelationProvider)
        {
        }

        public SubjectServiceBus(IScheduler scheduler, IRequestCorrelationProvider<TRequest, TResponse> requestCorrelationProvider)
        {
            _requestCorrelationProvider = requestCorrelationProvider;
            _commands = Subject.Synchronize(new Subject<TCommand>());
            _events = Subject.Synchronize(new Subject<TEvent>());
            _requests = Subject.Synchronize(new Subject<TRequest>());
            _responses = Subject.Synchronize(new Subject<TResponse>());

            Events = scheduler == null
                ? _events.AsObservable()
                : _events.ObserveOn(scheduler)
                    .PublishRefCountRetriable()
                    .AsObservable();

            Commands = scheduler == null
                ? _commands.AsObservable()
                : _commands.ObserveOn(scheduler)
                    .PublishRefCountRetriable()
                    .AsObservable();

            Requests = scheduler == null
                ? _requests.AsObservable()
                : _requests.ObserveOn(scheduler)
                    .PublishRefCountRetriable()
                    .AsObservable();
        }

        public void Dispose()
        {
            // synchronized subjects are anonymous subject underneath,
            // which don't implement IDisposable
        }

        public IObservable<TEvent> Events { get; }
        public IObservable<TRequest> Requests { get; }
        public IObservable<TCommand> Commands { get; }
        public IObservable<Exception> Exceptions => Observable.Empty<Exception>();

        public Task SendAsync(TCommand command)
        {
            _commands.OnNext(command);
            return Task.CompletedTask;
        }

        public Task SendAsync(IEnumerable<TCommand> commands)
        {
            return Task.WhenAll(commands.Select(SendAsync));
        }

        public IObservable<TResponse> GetResponses(TRequest request)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the SubjectServiceBus with a IRequestCorrelationProvider");
            }

            _requestCorrelationProvider.SetRequestCorrelationIds(request);

            return Observable.Create<TResponse>(obs =>
            {
                var disposable = _responses
                    .Where(r => _requestCorrelationProvider.AreCorrelated(request, r))
                    .Subscribe(obs);
                _requests.OnNext(request);
                return disposable;
            });
        }

        public IObservable<T> GetResponses<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>();
        }

        public IObservable<T> GetResponse<T>(TRequest request) where T : TResponse
        {
            return GetResponses(request).OfType<T>();
        }

        public Task PublishAsync(TEvent ev)
        {
            _events.OnNext(ev);
            return Task.CompletedTask;
        }

        public Task ReplyAsync(TRequest request, TResponse response)
        {
            if (_requestCorrelationProvider == null)
            {
                throw new InvalidOperationException("Please configure the SubjectServiceBus with a IRequestCorrelationProvider");
            }

            _requestCorrelationProvider.SetCorrelationIds(request, response);

            _responses.OnNext(response);

            return Task.CompletedTask;
        }
    }
}