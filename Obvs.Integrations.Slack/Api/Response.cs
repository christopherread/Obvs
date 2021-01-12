using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal abstract class Response
	{
		[DataMember(Name = "ok")]
		public bool OK { get; private set; }
	}
}
