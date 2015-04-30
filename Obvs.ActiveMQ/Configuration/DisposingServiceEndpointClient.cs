using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.ActiveMQ.Configuration
{
    internal class DisposingServiceEndpointClient : IServiceEndpointClient
    {
        private readonly IServiceEndpointClient _endpointClient;
        private readonly IDisposable _disposable;

        public DisposingServiceEndpointClient(IServiceEndpointClient endpointClient, IDisposable disposable)
        {
            _endpointClient = endpointClient;
            _disposable = disposable;
        }

        public void Dispose()
        {
            _endpointClient.Dispose();
            _disposable.Dispose();
        }

        public bool CanHandle(IMessage message)
        {
            return _endpointClient.CanHandle(message);
        }

        public Task SendAsync(ICommand command)
        {
            return _endpointClient.SendAsync(command);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return _endpointClient.GetResponses(request);
        }

        public IObservable<IEvent> Events
        {
            get { return _endpointClient.Events; }
        }
    }
}