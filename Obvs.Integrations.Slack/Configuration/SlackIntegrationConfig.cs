using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Configuration
{
    public class SlackIntegrationConfig : ICanSpecifySlackIntegrationToken
    {
        private readonly ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> _config;

        public SlackIntegrationConfig(ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> config)
        {
            _config = config;
        }

        public ICanAddEndpointOrLoggingOrCorrelationOrCreate<IMessage, ICommand, IEvent, IRequest, IResponse> ConnectUsingToken(string token)
        {
            _config.WithEndpoint(new SlackIntegration(token));
            return _config;
        }
    }
}