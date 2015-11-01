using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Configuration
{
    public interface ICanSpecifySlackIntegrationToken
    {
        ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> ConnectUsingToken(string token);
    }
}