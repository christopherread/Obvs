using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class Team : Entity
	{
		[DataMember(Name = "email_domain")]
		public string EmailDomain { get; private set; }
	}
}
