using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class GetSlackUsers : IRequest, ISlackIntegrationMessage
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }
}