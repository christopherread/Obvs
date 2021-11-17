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
                        connection.Url = "nats://192.168.99.100:32774"; // change to local Docker address:port that maps onto 4222
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
            bus.Events.Subscribe(Console.WriteLine);

            await bus.SendAsync(new TestCommand {Id = 1});

            var response = await bus.GetResponse<TestResponse>(new TestRequest());
            Console.WriteLine(response);

            await Task.Delay(TimeSpan.FromSeconds(1));

            ((IDisposable)bus).Dispose();
        }
    }
}
