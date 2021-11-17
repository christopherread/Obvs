using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class Channel : Entity
	{
		[DataMember(Name = "is_im")]
		public bool IsPrivate { get; private set; }

		[DataMember(Name = "is_member")]
		public bool IsMember { get; private set; }
	}
}
