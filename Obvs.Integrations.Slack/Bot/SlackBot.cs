using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;

namespace Obvs.Integrations.Slack.Bot
{
    internal interface ISlackBot : IDisposable
    {
        Task Connect();
        Task Disconnect();
        void RegisterHandler(Handler handler);
        Channel[] GetChannels();
        User[] GetUsers();
    }

    internal class SlackBot : Bot, ISlackBot
    {
		private readonly ISlackRestApi _api;
		private ClientWebSocket _webSocket;
        private bool _saidHello;

        private User _self;
        private readonly Dictionary<string, User> _users = new Dictionary<string, User>(); // TODO: Handle new users joining/leaving
        private readonly Dictionary<string, Channel> _channels = new Dictionary<string, Channel>(); // TODO: Handle new channels/deleted
        private Task _listenTask;
        private CancellationTokenSource _cancellation;

        private SlackBot(string token)
		{
			_api = new SlackRestApi(token);
		}

		public static async Task<SlackBot> Connect(string apiToken)
		{
			// Can't do async constructors, so do connection here. This makes it easy to tie the lifetime of the
			// websocket to this class.
			var bot = new SlackBot(apiToken);
			await bot.Connect();
			return bot;
		}

	    public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
		    if (disposing)
		    {
		        if (_cancellation != null)
		        {
		            _cancellation.Cancel();
		        }

		        if (_listenTask != null)
		        {
		            _listenTask.Wait(3000);
		        }

		        if (_webSocket != null)
		        {
		            _webSocket.Dispose();
		        }
		    }
		}

        private Task<AuthTestResponse> AuthTest() => _api.Post<AuthTestResponse>("auth.test");

        private Task<RtmStartResponse> RtmStart() => _api.Post<RtmStartResponse>("rtm.start");

        private Task<PostMessageResponse> PostMessage(string channelId, string text, Attachment[] attachments = null) =>
			_api.Post<PostMessageResponse>("chat.postMessage", new Dictionary<string, string> {
				{ "as_user", "true" },
				{ "channel", channelId },
				{ "text", text },
				{ "attachments", attachments != null ? Serialiser.Serialise(attachments) : "" }
			});

	    public async Task Connect()
		{
	        if (_webSocket != null)
	        {
	            _webSocket.Dispose();
	        }

	        _webSocket = new ClientWebSocket();

            // First check we can authenticate.
            var authResponse = await AuthTest();
			Debug.WriteLine("Authorised as " + authResponse.User);

			// Issue a request to start a real time messaging session.
			var rtmStartResponse = await RtmStart();

			// Store users and channels so we can look them up by ID.
			_self = rtmStartResponse.Self;

            _users.Clear();
	        foreach (var user in rtmStartResponse.Users)
	        {
	            _users.Add(user.ID, user);
	        }

            _channels.Clear();
	        foreach (var channel in rtmStartResponse.Channels.Union(rtmStartResponse.IMs))
	        {
	            _channels.Add(channel.ID, channel);
	        }
			// Connect the WebSocket to the URL we were given back.
			await _webSocket.ConnectAsync(rtmStartResponse.Url, CancellationToken.None);
			Debug.WriteLine("Connected...");

            // Start the receive message loop.
            _cancellation = new CancellationTokenSource();
            _listenTask = Task.Factory.StartNew(() => ListenForApiMessages(_cancellation.Token), _cancellation.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

	        if (_saidHello)
	            return;

			// Say hello in each of the channels the bot is a member of.
	        foreach (var channel in _channels.Values.Where(c => !c.IsPrivate && c.IsMember))
	        {
	            await SayHello(channel);
	            _saidHello = true;
	        }
		}

		public async Task Disconnect()
		{
			// Cancel all in-process tasks.
			await CancelAllTasks();

			// Say goodbye to each of the channels the bot is a member of.
		    foreach (var channel in _channels.Values.Where(c => !c.IsPrivate && c.IsMember))
		    {
		        await SayGoodbye(channel);
		    }
		}

        public Channel[] GetChannels()
        {
            return _channels.Values.ToArray();
        }

        public User[] GetUsers()
        {
            return _users.Values.ToArray();
        }

        private async Task ListenForApiMessages(CancellationToken ct)
		{
            try
            {
                var buffer = new byte[1024];
                var segment = new ArraySegment<byte>(buffer);
                while (!ct.IsCancellationRequested)
                {
                    var fullMessage = new StringBuilder();

                    WebSocketReceiveResult msg;

                    do
                    {
                        msg = await _webSocket.ReceiveAsync(segment, ct);

                        fullMessage.Append(Encoding.UTF8.GetString(buffer, 0, msg.Count));

                    } while (!msg.EndOfMessage);

                    await HandleApiMessage(fullMessage.ToString());
                }
            }
            catch (Exception ex)
            {
				Debug.WriteLine(ex.ToString());

                if (!ct.IsCancellationRequested)
                {
                    // Incase it's a temporary network blip.
                    await Task.Delay(2500, ct);

                    if (!ct.IsCancellationRequested)
                    {
                        await Connect();
                    }
                }
            }
		}

	    internal override async Task SendMessage(Channel channel, string text, Attachment[] attachments = null)
		{
			try
			{
				await PostMessage(channel.ID, text, attachments);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
		}

		internal override async Task SendTypingIndicator(Channel channel)
		{
			await SendApiMessage(new TypingIndicator(channel.ID));
		}

		internal async Task SendApiMessage<T>(T message)
		{
			var json = Serialiser.Serialise(message);
			Debug.WriteLine("SEND: " + json);
			await _webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
		}

        private async Task HandleApiMessage(string message)
		{
			Debug.WriteLine("RCV: " + message);

			var eventType = Serialiser.Deserialise<Event>(message).Type;

			switch (eventType)
			{
				case MessageEvent.TYPE:
					await HandleApiMessage(Serialiser.Deserialise<MessageEvent>(message));
					break;

				case ChannelChangedEvent.CHANNEL_CHANGED_TYPE:
				case ChannelChangedEvent.CHANNEL_CREATED_TYPE:
					HandleApiMessage(Serialiser.Deserialise<ChannelChangedEvent>(message));
					break;

				case UserChangedEvent.USER_CHANGED_TYPE:
				case UserChangedEvent.USER_CREATED_TYPE:
					HandleApiMessage(Serialiser.Deserialise<UserChangedEvent>(message));
					break;

				case ChannelJoinedEvent.TYPE:
					await HandleApiMessage(Serialiser.Deserialise<ChannelJoinedEvent>(message));
					break;
			}
		}

        private Task HandleApiMessage(MessageEvent message)
		{
			var channelId = message.Message?.ChannelID ?? message.ChannelID;
			var userId = message.Message?.UserID ?? message.UserID;
			var text = message.Message?.Text ?? message.Text;
            
		    var messageIsFromBot = userId == _self.ID;
		    if (messageIsFromBot)
		    {
			    return Task.FromResult(true);
            }

			var botIsMentioned = text.Contains($"<@{_self.ID}>");

			HandleRecievedMessage(_channels[channelId], _users[userId], text, botIsMentioned);

			return Task.FromResult(true);
		}

        private async Task HandleApiMessage(ChannelJoinedEvent message)
		{
			Debug.WriteLine("JOINED: " + message.Channel.Name);

			await SayHello(message.Channel);
		}

        private void HandleApiMessage(ChannelChangedEvent message)
		{
			Debug.WriteLine("CHANNEL UPDATED: " + message.Channel.Name);

			_channels[message.Channel.ID] = message.Channel;
		}

        private void HandleApiMessage(UserChangedEvent message)
		{
			Debug.WriteLine("USER UPDATED: " + message.User.Name);

			_users[message.User.ID] = message.User;
		}
	}
}
