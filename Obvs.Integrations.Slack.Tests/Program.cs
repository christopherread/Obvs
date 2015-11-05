using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.Integrations.Slack.Configuration;
using Obvs.Integrations.Slack.Messages;

namespace Obvs.Integrations.Slack.Tests
{
    public class Program
    {
        static void Main(string[] args)
        {
            const string token = "xoxb-13657478324-zaLy8ERhYFl4plNtNOM5OvWb";

            var serviceBus = ServiceBus.Configure()
                .WithEndpoint(new FakeEndpoint())
                .WithSlackIntegration()
                    .ConnectUsingToken(token)
                .Create();
           
            var sub = serviceBus.Events.OfType<SlackMessageReceived>().Subscribe(msg =>
            {
                Console.WriteLine(msg);
                serviceBus.SendAsync(new SendSlackMessage
                {
                    ChannelId = msg.ChannelId,
                    Text = null,
                    Attachments =
                        new List<SendSlackMessage.Attachment>
                        {
                            new SendSlackMessage.Attachment
                            {
                                Title = "Message Received",
                                Colour = "good",
                                Pretext = "Testing message attachments",
                                Fallback = "My fallback text",
                                Text = "This is where you write the main body of the message",
                                Fields = new []
                                {
                                    new SendSlackMessage.Field {Short = true, Title = "User", Value = $"@{msg.UserName}"},
                                    new SendSlackMessage.Field {Short = true, Title = "Time", Value = DateTime.Now.ToString("HH:mm:ss")},
                                    new SendSlackMessage.Field {Short = true, Title = "Channel", Value = $"#{msg.ChannelName}"}
                                }
                            }
                        }
                });
            });

            Console.WriteLine("Hit enter to exit.");
            Console.ReadLine();

            sub.Dispose();
            ((IDisposable)serviceBus).Dispose();
        } 
    }
}