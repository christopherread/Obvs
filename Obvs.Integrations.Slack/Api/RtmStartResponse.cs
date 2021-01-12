using System;
using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class RtmStartResponse : Response
	{
		[DataMember(Name = "url")]
		public Uri Url { get; private set; }

		[DataMember(Name = "self")]
		public User Self { get; private set; }

		[DataMember(Name = "team")]
		public Team Team { get; private set; }

		[DataMember(Name = "users")]
		public User[] Users { get; private set; }

		[DataMember(Name = "channels")]
		public Channel[] Channels { get; private set; }

		[DataMember(Name = "groups")]
		public Group[] Groups { get; private set; }

		[DataMember(Name = "ims")]
		public IM[] IMs { get; private set; }

		[DataMember(Name = "bots")]
		public BotUser[] Bots { get; private set; }
	}
}
