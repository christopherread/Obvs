using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Messages;
using Obvs.Types;
using SimpleSlackBot;

namespace Obvs.Integrations.Slack
{
    public class SlackIntegration : Handler, IServiceEndpointClient
    {
        private readonly Subject<IEvent> _events = new Subject<IEvent>();
        private readonly ConcurrentDictionary<string, Channel> _channels = new ConcurrentDictionary<string, Channel>();
        private SlackBot _bot;
        private bool _connected;
        private readonly object _connectLock = new object();
        private readonly string _token;

        public SlackIntegration(string token)
        {
            Name = GetType().Name;
            _token = token;
        }

        private void Connect()
        {
            if (!_connected)
            {
                lock (_connectLock)
                {
                    if (!_connected)
                    {
                        _bot = SlackBot.Connect(_token).Result;
                        _bot.RegisterHandler(this);
                        _connected = true;
                    }
                }
            }
        }

        private void Disconnect()
        {
            if (_connected)
            {
                lock (_connectLock)
                {
                    if (_connected)
                    {
                        _bot?.Disconnect().Wait();
                        _bot?.Dispose();
                        _connected = false;
                    }
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
            _channels.Clear();
            _events.Dispose();
        }

        public bool CanHandle(IMessage message)
        {
            return message is ISlackIntegrationMessage;
        }

        public string Name { get; }

        public async Task SendAsync(ICommand command)
        {
            Connect();

            var sendSlackMessage = command as SendSlackMessage;
            if (sendSlackMessage != null)
            {
                Channel channel;
                if (_channels.TryGetValue(sendSlackMessage.ChannelId, out channel))
                {
                    await SendMessage(channel, sendSlackMessage.Text);
                }
                else
                {
                    throw new Exception($"Unknown Channel ID '{sendSlackMessage.ChannelId}'");
                }
            }
            else
            {
                throw new Exception($"Unknown command type '{command.GetType().FullName}'");
            }
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            Connect();

            var getSlackChannels = request as GetSlackChannels;
            if (getSlackChannels != null)
            {
                return Observable.Return(new GetSlackChannelsResponse
                {
                    RequestId = request.RequestId,
                    RequesterId = request.RequesterId,
                    Channels = _channels.Values.Select(c =>
                        new GetSlackChannelsResponse.Channel
                        {
                            Id = c.ID,
                            Name = c.Name,
                            IsPrivate = c.IsPrivate,
                            IsMember = c.IsMember
                        }).ToList()
                });
            }
            throw new Exception($"Unknown request type '{request.GetType().FullName}'");
        }

        public IObservable<IEvent> Events
        {
            get
            {
                Connect();
                return _events;
            }
        }

        public override async Task OnMessage(Channel channel, User user, string text, bool botIsMentioned)
        {
            _channels.AddOrUpdate(channel.ID, channel, (s, c) => channel);

            _events.OnNext(new SlackMessageReceived
            {
                ChannelId = channel.ID,
                ChannelName = channel.Name,
                IsPrivate = channel.IsPrivate,
                IsMember = channel.IsMember,
                Text = text,
                IsBotMentioned = botIsMentioned,
                UserName = user.Name,
                UserId = user.ID
            });

            await Task.FromResult(true);
        }
    }
}
