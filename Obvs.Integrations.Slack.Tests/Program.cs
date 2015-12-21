using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Obvs.Configuration;
using Obvs.Integrations.Slack.Configuration;
using Obvs.Integrations.Slack.Messages;

namespace Obvs.Integrations.Slack.Tests
{
    public class Program
    {
        static void Main(string[] args)
        {
            const string token = "<insert your token here>";

            var serviceBus = ServiceBus.Configure()
                .WithEndpoint(new FakeEndpoint())
                .WithSlackIntegration()
                    .ConnectUsingToken(token)
                .Create();

            var sub1 = serviceBus.Events.Subscribe(Console.WriteLine);

            var sub2 = serviceBus.Events.OfType<SlackMessageReceived>().Subscribe(msg =>
            {
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

            serviceBus.GetResponses<GetSlackChannelsResponse>(new GetSlackChannels())
                .Subscribe(r =>
                {
                    var channels = r.Channels.Select(c => $"#{c.Name}").ToArray();
                    Console.WriteLine(string.Join("\n", channels));
                });

            serviceBus.GetResponses<GetSlackUsersResponse>(new GetSlackUsers())
                .Subscribe(r =>
                {
                    var users = r.Users.Select(user => $"@{user.Name}").ToArray();
                    Console.WriteLine(string.Join("\n", users));
                });

            Console.WriteLine("Hit enter to exit.");
            Console.ReadLine();

            sub1.Dispose();
            sub2.Dispose();
            ((IDisposable)serviceBus).Dispose();
        } 
    }
}