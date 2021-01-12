using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
	internal class TypingIndicator
	{
		public const string TYPING = "typing";

		[DataMember(Name = "type")]
		public string Type { get; private set; } = TYPING;

		[DataMember(Name = "channel")]
		public string ChannelID { get; private set; }

		public TypingIndicator(string channelID)
		{
			this.ChannelID = channelID;
		}
	}
}
