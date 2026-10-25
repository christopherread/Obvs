using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Obvs.Configuration;
using Obvs.NATS.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;
using Xunit;

namespace Obvs.NATS.Tests
{
    public class TestServiceBus
    {
        [Fact, Trait("Category", "Explicit")]
        public async Task TestBasicSendReceiveOfAllMessageTypes()
        {
            var bus = ServiceBus.Configure()
                .WithNatsEndpoint<ITestService, IMessage, ICommand, IEvent, IRequest, IResponse>(settings =>
                {
                    settings.ServiceName = "Obvs.NATS.TestService";
                    settings.Configure(connection =>
                    {
                        connection.Url = TestBroker.Url;
                        connection.IsShared = true;
                    });
                    settings.Configure(messageProperty =>
                    {
                        messageProperty.Filter = properties => properties.ContainsKey("blah");
                        messageProperty.Provider = message => new Dictionary<string, string> { { "blah", "foo" } };
                    });     
                }).SerializedAsJson().AsClientAndServer()
                .Create();

            bus.Commands.OfType<TestCommand>().Subscribe(command =>
            {
                Console.WriteLine(command);
                bus.PublishAsync(new TestEvent {Id = command.Id});
            });
            bus.Requests.OfType<TestRequest>().Subscribe(request =>
            {
                Console.WriteLine(request);
                bus.ReplyAsync(request, new TestResponse {Id = request.Id});
            });
            var events = new List<IEvent>();
            bus.Events.Subscribe(events.Add);

            await bus.SendAsync(new TestCommand {Id = 1});

            var response = await bus.GetResponse<TestResponse>(new TestRequest {Id = 2});
            Assert.Equal(2, response.Id);

            await Task.Delay(TimeSpan.FromSeconds(1));

            ((IDisposable)bus).Dispose();

            Assert.Contains(events, ev => ev is TestEvent testEvent && testEvent.Id == 1);
        }
    }
}
