using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class SlackMessageReceived : IEvent, ISlackIntegrationMessage
    {
        public string ChannelId { get; set; }
        public string ChannelName { get; set; }
        public string Text { get; set; }
        public bool IsBotMentioned { get; set; }
        public string UserName { get; set; }
        public string UserId { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsMember { get; set; }
    }
}