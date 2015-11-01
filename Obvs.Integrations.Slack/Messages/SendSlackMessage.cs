using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class SendSlackMessage : ICommand, ISlackIntegrationMessage
    {
        public string Text { get; set; }
        public string ChannelId { get; set; }
    }
}