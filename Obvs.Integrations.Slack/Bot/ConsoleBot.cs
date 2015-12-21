using System;
using System.Linq;
using System.Threading.Tasks;
using Obvs.Integrations.Slack.Api;

namespace Obvs.Integrations.Slack.Bot
{
    internal class ConsoleBot : Bot
	{
	    readonly Channel _consoleChannel = new Channel { Name = "Console" };
	    readonly User _consoleUser = new User { Name = "Console" };

		internal override Task SendMessage(Channel channel, string text, Attachment[] attachments = null)
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine(text);
			if (attachments != null && attachments.Any())
			{
				foreach (var attachment in attachments)
				{
					if (!string.IsNullOrEmpty(attachment.Pretext))
						Console.WriteLine(attachment.Pretext);
					if (!string.IsNullOrEmpty(attachment.AuthorName))
						Console.WriteLine(attachment.AuthorName);
					Console.WriteLine($"{attachment.Title} <{attachment.TitleLink}>");
					if (!string.IsNullOrEmpty(attachment.Text))
						Console.WriteLine(attachment.Text);
					foreach (var field in attachment.Fields)
						Console.WriteLine($"{field.Title}: {field.Value}");
					Console.WriteLine();
				}
			}
			Console.ResetColor();

			return Task.FromResult(true);
		}

		internal override Task SendTypingIndicator(Channel channel)
		{
			return Task.FromResult(true);
		}

		public void HandleInput()
		{
			SayHello(_consoleChannel).Wait();

			while (true)
			{
				var text = Console.ReadLine();
				Console.WriteLine();

				if (string.Equals(text, "quit"))
				{
					SayGoodbye(_consoleChannel).Wait();
					break;
				}

				HandleRecievedMessage(_consoleChannel, _consoleUser, text, text != null && text.Contains("@bot"));
			}
		}
	}
}
