using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using Obvs.Serialization;
using Obvs.Serialization.ProtoBuf;
using Obvs.Types;
using ProtoBuf;

namespace Obvs.NetMQ.Tests.Console.Subscriber
{
	class Program
	{
		static void Main(string[] args)
		{
			string endPoint = "tcp://localhost:5557";
			System.Console.WriteLine("Listening on {0}\n", endPoint);

			const string topic = "TestTopicxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

			IDisposable sub;
			{
				var source = new MessageSource<IMessage>(endPoint,
					new IMessageDeserializer<IMessage>[]
					{
						new ProtoBufMessageDeserializer<Message1AndItIs32CharactersLongForSureDefinitionForSure>(),
					},
					topic);

				sub = source.Messages.Subscribe(msg =>
					{
						System.Console.WriteLine("Received: " + msg);
					},
				   err => System.Console.WriteLine("Error: " + err));
			}

			System.Console.ReadKey();
		}
	}

	[ProtoContract]
	public class Message1AndItIs32CharactersLongForSureDefinitionForSure : IMessage
	{
		public Message1AndItIs32CharactersLongForSureDefinitionForSure()
		{

		}

		[ProtoMember(1)]
		public int Id { get; set; }

		public override string ToString()
		{
			return "Message1AndItIs32CharactersLongForSureDefinitionForSure-" + Id;
		}
	}
}
