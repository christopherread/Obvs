namespace Obvs.Integrations.Slack.Messages
{
    public interface ISlackIntegrationMessage
    {
    }

    public interface IHasSlackChannelDetails : ISlackChannel
    {
    }

    public interface ISlackChannel
    {
        string ChannelId { get; }
        string ChannelName { get; }
    }

    public interface IHasSlackSenderDetails : ISlackUser
    {
    }

    public interface ISlackUser
    {
        string UserName { get; }
        string UserId { get; }
    }
}