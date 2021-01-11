using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class MessageEvent : Event
	{
		public const string TYPE = "message";
		public override string Type { get { return TYPE; } }

		[DataMember(Name = "channel")]
		public string ChannelID { get; private set; }

		[DataMember(Name = "user")]
		public string UserID { get; private set; }

		[DataMember(Name = "text")]
		public string Text { get; private set; }

		[DataMember(Name = "message")]
		public MessageEvent Message { get; private set; }

		[DataMember(Name = "hidden")]
		public bool Hidden { get; private set; }
	}
}
