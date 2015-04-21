using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Tests
{
    public class FakeServiceEndpoint : IServiceEndpointClient, IServiceEndpoint
    {
        private readonly Subject<IMessage> _subject = new Subject<IMessage>();
        private readonly Type _serviceType;
        public bool ThrowException { get; set; }

        public FakeServiceEndpoint(Type serviceType)
        {
            _serviceType = serviceType;
        }

        public bool CanHandle(IMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }

        public Task SendAsync(ICommand command)
        {
            _subject.OnNext(command);
            return Task.FromResult(true);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Create<IResponse>(observer =>
                _subject.OfType<IResponse>().Where(response => response.RequestId == request.RequestId && response.RequesterId == request.RequesterId).Select(ev =>
                {
                    if (ThrowException)
                    {
                        throw new Exception();
                    }
                    return ev;
                }).Subscribe(observer));
        }

        public IObservable<IEvent> Events
        {
            get
            {
                return Observable.Create<IEvent>(observer =>
                    _subject.OfType<IEvent>().Select(ev =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return ev;
                    }).Subscribe(observer));
            }
        }

        public IObservable<IRequest> Requests
        {
            get
            {
                return Observable.Create<IRequest>(observer =>
                    _subject.OfType<IRequest>().Select(request =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return request;
                    }).Subscribe(observer));
            }
        }

        public IObservable<ICommand> Commands
        {
            get
            {
                return Observable.Create<ICommand>(observer =>
                    _subject.OfType<ICommand>().Select(command =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return command;
                    }).Subscribe(observer));
            }
        }

        public Task PublishAsync(IEvent ev)
        {
            _subject.OnNext(ev);
            return Task.FromResult(true);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
            _subject.OnNext(response);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            _subject.Dispose();
        }
    }
}