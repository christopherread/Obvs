using System.Collections.Generic;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Messages
{
    public class SendSlackMessage : ICommand, ISlackIntegrationMessage
    {
        public string Text { get; set; }
        public List<Attachment> Attachments { get; set; }
        public string ChannelId { get; set; }

        public SendSlackMessage()
        {
            Attachments = new List<Attachment>();
        }

        public class Attachment
        {
            public string Fallback { get; set; }
            public string Colour { get; set; }
            public string Pretext { get; set; }
            public string AuthorName { get; set; }
            public string AuthorLink { get; set; }
            public string AuthorIcon { get; set; }
            public string Title { get; set; }
            public string TitleLink { get; set; }
            public string Text { get; set; }
            public Field[] Fields { get; set; }
            public string ImageUrl { get; set; }
            public string ThumbUrl { get; set; }
        }

        public class Field
        {
            public string Title { get; set; }
            public string Value { get; set; }
            public bool Short { get; set; }
        }
    }
}