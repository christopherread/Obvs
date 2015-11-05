using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Integrations.Slack.Configuration;
using Obvs.Integrations.Slack.Messages;
using Obvs.Types;

namespace Obvs.Integrations.Slack.Tests
{
    [TestFixture]
    public class TestSlackIntegration
    {
        [Test, Explicit]
        public async void TestIntegration()
        {
            const string token = "xoxb-13657478324-zaLy8ERhYFl4plNtNOM5OvWb";

            var serviceBus = ServiceBus.Configure()
                .WithEndpoint(new FakeEndpoint())
                .WithSlackIntegration()
                    .ConnectUsingToken(token)
                .Create();

            var sub = serviceBus.Events.OfType<SlackMessageReceived>().Subscribe(Console.WriteLine);
            var sub2 = serviceBus.Events.OfType<SlackMessageReceived>().Subscribe(msg =>
                serviceBus.SendAsync(new SendSlackMessage
                {
                    ChannelId = msg.ChannelId,
                    Text = "Message received"
                }));

            await Task.Delay(TimeSpan.FromSeconds(30));

            sub.Dispose();
            sub2.Dispose();
            ((IDisposable)serviceBus).Dispose();
        }
    }

    public class FakeEndpoint : IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>
    {
        public FakeEndpoint()
        {
            Name = "FakeEndpoint";
        }

        public void Dispose()
        {
        }

        public bool CanHandle(IMessage message)
        {
            return false;
        }

        public string Name { get; }
        public Task SendAsync(ICommand command)
        {
            return Task.FromResult(true);
        }

        public IObservable<IResponse> GetResponses(IRequest request)
        {
            return Observable.Empty<IResponse>();
        }

        public IObservable<IEvent> Events => Observable.Empty<IEvent>();
    }
}
