using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class Entity
	{
		[DataMember(Name = "id")]
		public string ID { get; internal set; }

		[DataMember(Name = "name")]
		public string Name { get; internal set; }
	}
}
