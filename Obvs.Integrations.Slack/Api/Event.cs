using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class Event
	{
		[DataMember(Name = "type")]
		public virtual string Type { get; protected set; }

		[DataMember(Name = "subtype")]
		public string SubType { get; protected set; }
	}
}
