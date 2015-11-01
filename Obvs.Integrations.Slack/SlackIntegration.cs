using System;
using System.Threading.Tasks;
using Obvs.Types;

namespace Obvs.Integrations.Slack
{
    public class SlackIntegration : IServiceEndpointClient
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool CanHandle(IMessage message)
        {
            throw new NotImplementedException();
        }

        public string Name { get; }
        public Task SendAsync(ICommand command)
        {
            throw new NotImplementedException();
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            throw new NotImplementedException();
        }

        public IObservable<IEvent> Events { get; private set; }
    }
}
