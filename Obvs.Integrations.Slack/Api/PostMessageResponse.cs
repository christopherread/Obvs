using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class PostMessageResponse : Response
	{
		[DataMember(Name = "ts")]
		public string Timestamp{ get; private set; }
	}
}
