using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;

namespace Obvs.Integrations.Slack.Bot
{
	internal abstract class Bot
	{
		private readonly HashSet<Handler> _handlers = new HashSet<Handler>();
        private readonly ConcurrentBag<Task> _handlerTasks = new ConcurrentBag<Task>();
        private CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private readonly string[] _cancellationTerms = { "cancel", "abort", "stop" };
		
		public void RegisterHandler(Handler handler)
		{
			handler.SetBot(this);
			_handlers.Add(handler);
		}

        internal abstract Task SendMessage(Channel channel, string text, Attachment[] attachments = null);

		internal abstract Task SendTypingIndicator(Channel channel);

		protected void HandleRecievedMessage(Channel channel, User user, string text, bool botIsMentioned)
		{
			// If the text is cancellation, then send a cancellation message instead.
			if (_cancellationTerms.Contains(text, StringComparer.OrdinalIgnoreCase))
			{
				_cancellationSource.Cancel();
				_cancellationSource = new CancellationTokenSource();
				return;
			}

		    foreach (var handler in _handlers)
		    {
		        _handlerTasks.Add(SendMessageToHandlerAsync(channel, user, text, botIsMentioned, handler));
		    }
		}

        private async Task SendMessageToHandlerAsync(Channel channel, User user, string text, bool botIsMentioned, Handler handler)
		{
			try
			{
				await handler.OnMessage(channel, user, text, botIsMentioned, _cancellationSource.Token);
			}
			catch (Exception ex)
			{
				await SendMessage(channel, ex.ToString());
			}
		}

		protected async Task CancelAllTasks()
		{
			_cancellationSource.Cancel();

			// Wait for all tasks to finish cancelling.
			await Task.WhenAll(_handlerTasks);
		}

		protected async Task SayHello(Channel channel)
		{
			await SendMessage(channel, RandomMessages.Hello());
		}

		protected async Task SayGoodbye(Channel channel)
		{
			await SendMessage(channel, RandomMessages.Goodbye());
		}
	}
}
