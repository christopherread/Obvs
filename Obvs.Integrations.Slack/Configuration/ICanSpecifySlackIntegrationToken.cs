using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Configuration
{
    public interface ICanSpecifySlackIntegrationToken
    {
        ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> ConnectUsingToken(string token);
    }
}