using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Tests
{
    public class FakeServiceEndpoint : IServiceEndpointClient, IServiceEndpoint
    {
        public readonly Subject<IMessage> Messages = new Subject<IMessage>();
        private readonly Type _serviceType;
        public bool ThrowException { private get; set; }

        public FakeServiceEndpoint(Type serviceType)
        {
            _serviceType = serviceType;
        }

        public bool CanHandle(IMessage message)
        {
            return _serviceType.IsInstanceOfType(message);
        }

        public string Name
        {
            get { return GetType().FullName; }
        }

        public Task SendAsync(ICommand command)
        {
            Messages.OnNext(command);
            return Task.FromResult(true);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Create<IResponse>(observer =>
            {
                var disposable = Messages.OfType<IResponse>()
                    .Where(response => response.RequestId == request.RequestId &&
                                       response.RequesterId == request.RequesterId)
                    .Select(ev =>
                    {
                        if (ThrowException)
                        {
                            throw new Exception();
                        }
                        return ev;
                    }).Subscribe(observer);

                Messages.OnNext(request);

                return disposable;
            });
        }

        public IObservable<IEvent> Events
        {
            get
            {
                return Observable.Create<IEvent>(observer =>
                    Messages.OfType<IEvent>().Select(ev =>
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
                    Messages.OfType<IRequest>().Select(request =>
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
                    Messages.OfType<ICommand>().Select(command =>
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
            Messages.OnNext(ev);
            return Task.FromResult(true);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            response.RequestId = request.RequestId;
            response.RequesterId = request.RequesterId;
            Messages.OnNext(response);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            Messages.Dispose();
        }
    }
}