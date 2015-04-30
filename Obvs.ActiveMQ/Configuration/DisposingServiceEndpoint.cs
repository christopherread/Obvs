using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    internal class DisposingServiceEndpoint : IServiceEndpoint
    {
        private readonly IServiceEndpoint _endpoint;
        private readonly IDisposable _disposable;

        public DisposingServiceEndpoint(IServiceEndpoint endpoint, IDisposable disposable)
        {
            _endpoint = endpoint;
            _disposable = disposable;
        }

        public void Dispose()
        {
            _endpoint.Dispose();
            _disposable.Dispose();
        }

        public bool CanHandle(IMessage message)
        {
            return _endpoint.CanHandle(message);
        }

        public Task PublishAsync(IEvent ev)
        {
            return _endpoint.PublishAsync(ev);
        }

        public Task ReplyAsync(IRequest request, IResponse response)
        {
            return _endpoint.ReplyAsync(request, response);
        }

        public IObservable<IRequest> Requests
        {
            get { return _endpoint.Requests; }
        }

        public IObservable<ICommand> Commands
        {
            get { return _endpoint.Commands; }
        }
    }
}