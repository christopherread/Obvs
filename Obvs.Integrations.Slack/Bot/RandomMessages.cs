using System;

namespace Obvs.Integrations.Slack.Bot
{
    internal static class RandomMessages
	{
		static readonly Random Rnd = new Random();
		static bool Choose() => Rnd.Next(2) == 1;
        static string RandomOf(params string[] items)
		{
			var msg = items[Rnd.Next(items.Length)];

			if (Choose())
				msg = msg.ToLower();

			if (Choose())
				msg += "!";

			return msg;
		}

		public static string Hello() => RandomOf("Hello", "I'm back!", "Hi", "'sup?", "Yo");

		public static string Goodbye() => RandomOf("Goodbye", "Bye", "I'll be back", "Farewell", "So long");
	}
}
