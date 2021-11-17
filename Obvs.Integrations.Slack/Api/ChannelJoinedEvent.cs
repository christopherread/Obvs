using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class ChannelJoinedEvent : Event
	{
		public const string TYPE = "channel_joined";

		[DataMember(Name = "channel")]
		public Channel Channel { get; private set; }
	}
}
