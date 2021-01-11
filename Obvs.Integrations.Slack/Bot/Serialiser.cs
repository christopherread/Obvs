using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Obvs.Integrations.Slack.Bot
{
    internal static class Serialiser
	{
		public static T Deserialise<T>(string json)
		{
			var serialiser = new DataContractJsonSerializer(typeof(T));

			using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
			{
				ms.Position = 0;
				return (T)serialiser.ReadObject(ms);
			}
		}

		public static string Serialise<T>(T obj)
		{
			var serialiser = new DataContractJsonSerializer(typeof(T));
			using (MemoryStream ms = new MemoryStream())
			{
				serialiser.WriteObject(ms, obj);
				return Encoding.UTF8.GetString(ms.ToArray());
			}
		}
	}
}
