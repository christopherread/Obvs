using Obvs.Kafka.Configuration;
using Obvs.Serialization.Json;
using Obvs.Types;
using Xunit;

namespace Obvs.Kafka.Tests
{
    // How consumer groups are named. Names don't depend on the machine or process, so a restarted bus reuses its groups.
    // None of these connect to a broker.
    public class TestKafkaConsumerGroups
    {
        public interface ITestMessage : IMessage
        {
        }

        [Fact]
        public void ShouldShareOneGroupPerServiceForCommandsAndRequests()
        {
            // So each command and request is handled by one of the service's buses
            foreach (var provider in new[] { Provider("bus-a"), Provider("bus-b") })
            {
                Assert.Equal("orders.Commands", provider.CreateSource<ICommand>("orders.Commands").ConsumerConfig.GroupId);
                Assert.Equal("orders.Requests", provider.CreateSource<IRequest>("orders.Requests").ConsumerConfig.GroupId);
            }
        }

        [Fact]
        public void ShouldGiveEachBusItsOwnGroupForEventsAndResponses()
        {
            // So every bus receives every event, and the responses to its own requests
            var provider = Provider("bus-a");

            Assert.Equal("orders.Events.bus-a", provider.CreateSource<IEvent>("orders.Events").ConsumerConfig.GroupId);
            Assert.Equal("orders.Responses.bus-a", provider.CreateSource<IResponse>("orders.Responses").ConsumerConfig.GroupId);
            Assert.Equal("orders.Events.bus-b", Provider("bus-b").CreateSource<IEvent>("orders.Events").ConsumerConfig.GroupId);
        }

        [Fact]
        public void ShouldKeepV6sGroupNamedAfterTheProcessWithoutAnInstanceId()
        {
            // v6's fluent configuration, where every source in the process receives every message
            var provider = Provider(null);

            foreach (var topic in new[] { "orders.Commands", "orders.Events", "orders.Requests", "orders.Responses" })
            {
                var config = provider.CreateSource<IMessage>(topic).ConsumerConfig;
                Assert.Equal(config.ClientId, config.GroupId);
            }
        }

        private static KafkaServiceEndpointProvider<ITestMessage, IMessage, ICommand, IEvent, IRequest, IResponse> Provider(string instanceId)
        {
            return new KafkaServiceEndpointProvider<ITestMessage, IMessage, ICommand, IEvent, IRequest, IResponse>("orders",
                new KafkaConfiguration("kafka.example:9092"), null, null,
                new JsonMessageSerializer(), new JsonMessageDeserializerFactory(typeof(JsonMessageDeserializer<>)),
                null, null, null, null, instanceId);
        }
    }
}
