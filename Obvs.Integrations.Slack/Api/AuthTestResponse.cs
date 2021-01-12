using System;
using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class AuthTestResponse : Response
	{
		[DataMember(Name = "url")]
		public Uri Url { get; private set;  }

		[DataMember(Name = "team")]
		public string Team { get; private set; }

		[DataMember(Name = "user")]
		public string User { get; private set; }

		[DataMember(Name = "team_id")]
		public string TeamID { get; private set; }

		[DataMember(Name = "user_id")]
		public string UserID { get; private set; }
	}
}
