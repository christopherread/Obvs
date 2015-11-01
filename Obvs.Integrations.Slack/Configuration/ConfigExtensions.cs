using Obvs.Configuration;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Configuration
{
    public static class ConfigExtensions
    {
        public static ICanSpecifySlackIntegrationToken WithSlackIntegration(
            this ICanAddEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse> config)
        {
            return new SlackIntegrationConfig(config);
        }
    }
}