using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class ChannelChangedEvent : Event
	{
		public const string CHANNEL_CREATED_TYPE = "channel_created";
		public const string CHANNEL_CHANGED_TYPE = "channel_rename";

		[DataMember(Name = "channel")]
		public Channel Channel { get; private set; }
	}
}
