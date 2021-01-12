using System.Runtime.Serialization;

namespace Obvs.Integrations.Slack.Api
{
	[DataContract]
    internal class UserChangedEvent : Event
	{
		public const string USER_CREATED_TYPE = "team_join";
		public const string USER_CHANGED_TYPE = "user_change";

		[DataMember(Name = "user")]
		public User User { get; private set; }
	}
}
