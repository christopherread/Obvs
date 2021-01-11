using System.Collections.Generic;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class GetSlackUsersResponse : IResponse, ISlackIntegrationMessage
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
        public List<User> Users { get; set; }

        public GetSlackUsersResponse()
        {
            Users = new List<User>();
        }

        public class User
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}