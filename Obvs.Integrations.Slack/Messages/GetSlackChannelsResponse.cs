using System.Collections.Generic;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class GetSlackChannelsResponse : IResponse, ISlackIntegrationMessage
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
        public List<Channel> Channels { get; set; }

        public GetSlackChannelsResponse()
        {
            Channels = new List<Channel>();
        }

        public class Channel
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public bool IsPrivate { get; set; }
            public bool IsMember { get; set; }
        }
    }
}