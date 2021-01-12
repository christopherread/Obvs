using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;

namespace Obvs.Integrations.Slack.Bot
{
    internal abstract class Handler
	{
		Bot _bot;

		internal void SetBot(Bot bot)
		{
			if (_bot != null)
				throw new Exception("This handler belongs to another :(");

			_bot = bot;
		}

	    protected virtual Task OnMessage(Channel channel, User user, string text, bool botIsMentioned)
		{
			return Task.FromResult(true);
		}

		public virtual Task OnMessage(Channel channel, User user, string text, bool botIsMentioned, CancellationToken cancellationToken)
		{
			return OnMessage(channel, user, text, botIsMentioned);
		}

		protected async Task SendMessage(Channel channel, string text, Attachment[] attachments = null)
		{
			await _bot.SendMessage(channel, text, attachments);
		}

		protected async Task SendTypingIndicator(Channel channel)
		{
			await _bot.SendTypingIndicator(channel);
		}

		protected string Escape(object input)
		{
			// https://api.slack.com/docs/formatting
			return (input?.ToString() ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace("&", "&gt;");
		}

		protected string UrlEncode(object input)
		{
			return WebUtility.UrlEncode(input?.ToString() ?? "");
		}
	}
}
