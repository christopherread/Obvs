using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class ErrorResponse : Response
	{
		[DataMember(Name = "error")]
		public string Error { get; private set; }
	}
}
