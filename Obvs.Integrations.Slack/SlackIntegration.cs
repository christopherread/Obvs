using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;
using Obvs.Integrations.Slack.Bot;
using Obvs.Integrations.Slack.Messages;
using Obvs.Types;

namespace Obvs.Integrations.Slack
{
    internal class SlackIntegration : Handler, IServiceEndpointClient
    {
        private readonly Subject<IEvent> _events = new Subject<IEvent>();
        private readonly ConcurrentDictionary<string, Channel> _channels = new ConcurrentDictionary<string, Channel>();
        private readonly ConcurrentDictionary<string, User> _users = new ConcurrentDictionary<string, User>();
        private ISlackBot _bot;
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
                        OnConnected();
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
            _users.Clear();
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
                    await SendMessage(channel, sendSlackMessage.Text, GetAttachments(sendSlackMessage));
                }
                else
                {
                    throw new Exception($"Unknown Channel ID '{sendSlackMessage.ChannelId}'");
                }
            }
            else
            {
                throw new Exception($"Unknown Command Type '{command.GetType().FullName}'");
            }
        }

        private static Attachment[] GetAttachments(SendSlackMessage sendSlackMessage)
        {
            return sendSlackMessage.Attachments.Select(a => new Attachment
            {
                AuthorIcon = a.AuthorIcon,
                Text = a.Text,
                AuthorLink = a.AuthorLink,
                AuthorName = a.AuthorName,
                Colour = a.Colour,
                Fallback = a.Fallback,
                ImageUrl = a.ImageUrl,
                Fields = a.Fields.Select(f => new Field(f.Title, f.Value, f.Short)).ToArray(),
                Title = a.Title,
                Pretext = a.Pretext,
                ThumbUrl = a.ThumbUrl,
                TitleLink = a.TitleLink
            }).ToArray();
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
                    Channels = _channels.Values.Select(channel =>
                        new GetSlackChannelsResponse.Channel
                        {
                            Id = channel.ID,
                            Name = channel.Name,
                            IsPrivate = channel.IsPrivate,
                            IsMember = channel.IsMember
                        }).ToList()
                });
            }

            var getSlackUsers = request as GetSlackUsers;
            if (getSlackUsers != null)
            {
                return Observable.Return(new GetSlackUsersResponse
                {
                    RequestId = request.RequestId,
                    RequesterId = request.RequesterId,
                    Users = _users.Values.Select(user =>
                        new GetSlackUsersResponse.User
                        {
                            Id = user.ID,
                            Name = user.Name
                        }).ToList()
                });
            }

            throw new Exception($"Unknown Request Type '{request.GetType().FullName}'");
        }

        public IObservable<IEvent> Events
        {
            get
            {
                Connect();
                return _events;
            }
        }

        protected override async Task OnMessage(Channel channel, User user, string text, bool botIsMentioned)
        {
            _channels[channel.ID] = channel;

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

        private void OnConnected()
        {
            foreach (var channel in _bot.GetChannels())
            {
                _channels[channel.ID] = channel;
            }
            foreach (var user in _bot.GetUsers())
            {
                _users[user.ID] = user;
            }
        }
    }
}
