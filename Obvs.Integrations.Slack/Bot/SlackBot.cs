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
	internal class SlackBot : Bot, IDisposable
	{
		readonly SlackRestApi _api;
		readonly ClientWebSocket _ws = new ClientWebSocket();

		User _self;
		readonly Dictionary<string, User> _users = new Dictionary<string, User>(); // TODO: Handle new users joining/leaving
		readonly Dictionary<string, Channel> _channels = new Dictionary<string, Channel>(); // TODO: Handle new channels/deleted

		#region Construction

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

		#endregion

		#region IDisposable

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				_ws.Dispose();
		}

		#endregion

		#region REST Methods

		Task<AuthTestResponse> AuthTest() => _api.Post<AuthTestResponse>("auth.test");
		Task<RtmStartResponse> RtmStart() => _api.Post<RtmStartResponse>("rtm.start");
		Task<PostMessageResponse> PostMessage(string channelID, string text, Attachment[] attachments = null) =>
			_api.Post<PostMessageResponse>("chat.postMessage", new Dictionary<string, string> {
				{ "as_user", "true" },
				{ "channel", channelID },
				{ "text", text },
				{ "attachments", attachments != null ? Serialiser.Serialise(attachments) : "" }
			});

		#endregion

		public async Task Connect()
		{
			if (_ws.State == WebSocketState.Connecting || _ws.State == WebSocketState.Open)
				throw new InvalidOperationException("Not is already connected");

			// First check we can authenticate.
			var authResponse = await this.AuthTest();
			Debug.WriteLine("Authorised as " + authResponse.User);

			// Issue a request to start a real time messaging session.
			var rtmResponse = await this.RtmStart();

			// Store users and channels so we can look them up by ID.
			_self = rtmResponse.Self;
			foreach (var user in rtmResponse.Users)
				_users.Add(user.ID, user);
			foreach (var channel in rtmResponse.Channels.Union(rtmResponse.IMs))
				_channels.Add(channel.ID, channel);

			// Connect the WebSocket to the URL we were given back.
			await _ws.ConnectAsync(rtmResponse.Url, CancellationToken.None);
			Debug.WriteLine("Connected...");

			// Start the receive message loop.
			var _ = Task.Run(ListenForApiMessages);

			// Say hello in each of the channels the bot is a member of.
			foreach (var channel in _channels.Values.Where(c => !c.IsPrivate && c.IsMember))
				await SayHello(channel);
		}

		public async Task Disconnect()
		{
			// Cancel all in-process tasks.
			await CancelAllTasks();

			// Say goodbye to each of the channels the bot is a member of.
			foreach (var channel in _channels.Values.Where(c => !c.IsPrivate && c.IsMember))
				await SayGoodbye(channel);
		}

		async Task ListenForApiMessages()
		{
			var buffer = new byte[1024];
			var segment = new ArraySegment<byte>(buffer);
			while (_ws.State == WebSocketState.Open)
			{
				var fullMessage = new StringBuilder();

				while (true)
				{
					var msg = await _ws.ReceiveAsync(segment, CancellationToken.None);

					fullMessage.Append(Encoding.UTF8.GetString(buffer, 0, msg.Count));
					if (msg.EndOfMessage)
						break;
				}

				await HandleApiMessage(fullMessage.ToString());
			}
		}

		#region Message Handling

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
			await _ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None);
		}

		async Task HandleApiMessage(string message)
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

		async Task HandleApiMessage(MessageEvent message)
		{
			var channelId = message.Message?.ChannelID ?? message.ChannelID;
			var userId = message.Message?.UserID ?? message.UserID;
			var text = message.Message?.Text ?? message.Text;

			// If the message is from our bot, do not handle it.
			if (userId == _self.ID)
				return;

			var botIsMentioned = text.Contains($"<@{_self.ID}>");

			HandleRecievedMessage(_channels[channelId], _users[userId], text, botIsMentioned);

			await Task.FromResult(true);
		}

		async Task HandleApiMessage(ChannelJoinedEvent message)
		{
			Debug.WriteLine("JOINED: " + message.Channel.Name);

			await SayHello(message.Channel);
		}

		void HandleApiMessage(ChannelChangedEvent message)
		{
			Debug.WriteLine("CHANNEL UPDATED: " + message.Channel.Name);

			_channels[message.Channel.ID] = message.Channel;
		}

		void HandleApiMessage(UserChangedEvent message)
		{
			Debug.WriteLine("USER UPDATED: " + message.User.Name);

			_users[message.User.ID] = message.User;
		}

		#endregion
	}
}
