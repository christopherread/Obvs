using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Serialization;
using Obvs.Testing;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting
{
    public interface ICatalogueMessage : IMessage { }

    public class AddProduct : ICatalogueMessage, ICommand { }

    public class ProductAdded : ICatalogueMessage, IEvent { }

    public class GetPrice : ICatalogueMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class Price : ICatalogueMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    // Registrations startup rejects
    public class ShipProduct : IMessage, ICommand { }

    public class AddOrUpdateProduct : ICatalogueMessage, ICommand, IEvent { }

    public class ProductNote : ICatalogueMessage { }

    public abstract class ProductCommand : ICatalogueMessage, ICommand { }

    internal class ReindexProducts : ICatalogueMessage, ICommand { }

    public class TestAddMessages
    {
        [Fact]
        public void ShouldListTheRegisteredMessageTypes()
        {
            var service = Configure(service => service
                .AddMessages(typeof(AddProduct), typeof(ProductAdded))
                .AddMessages(typeof(GetPrice), typeof(Price), typeof(AddProduct)));

            Assert.Equal(new[] { typeof(AddProduct), typeof(ProductAdded), typeof(GetPrice), typeof(Price) }, service.Messages);
        }

        [Fact]
        public void ShouldCreateDeserializersForExactlyTheRegisteredTypesWithoutScanning()
        {
            var factory = new TypedFactory();
            var service = Configure(service => service
                .AddMessages(typeof(AddProduct), typeof(ProductAdded))
                .SerializedWith(new NullSerializer(), factory));

            var deserializers = service.DeserializerFactory.Create<IMessage, ICatalogueMessage>();

            Assert.Equal(new[] { "AddProduct", "ProductAdded" }, deserializers.Select(deserializer => deserializer.GetTypeName()).OrderBy(name => name));
            Assert.Equal(0, factory.Scans);
        }

        [Fact]
        public void ShouldOnlyCreateDeserializersForRegisteredTypesOfTheKindAsked()
        {
            // Transports ask for each kind separately, such as the commands they receive
            var service = Configure(service => service
                .AddMessages(typeof(AddProduct), typeof(ProductAdded), typeof(GetPrice))
                .SerializedWith(new NullSerializer(), new TypedFactory()));

            var deserializers = service.DeserializerFactory.Create<ICommand, ICatalogueMessage>();

            Assert.Equal("AddProduct", Assert.Single(deserializers).GetTypeName());
        }

        [Fact]
        public void ShouldLimitTheScanToRegisteredTypesForFactoriesThatOnlyScan()
        {
            var service = Configure(service => service
                .AddMessages(typeof(AddProduct))
                .SerializedWith(new NullSerializer(), new ScanningFactory(typeof(AddProduct), typeof(ProductAdded), typeof(GetPrice))));

            var deserializers = service.DeserializerFactory.Create<IMessage, ICatalogueMessage>();

            Assert.Equal("AddProduct", Assert.Single(deserializers).GetTypeName());
        }

        [Fact]
        public void ShouldScanAsBeforeWhenNoMessagesAreRegistered()
        {
            var factory = new TypedFactory();
            var service = Configure(service => service.SerializedWith(new NullSerializer(), factory));

            Assert.Empty(service.Messages);
            Assert.Same(factory, service.DeserializerFactory);
        }

        [Fact]
        public void ShouldRejectANullType()
        {
            Assert.Throws<ArgumentNullException>(() => Configure(service => service.AddMessages(typeof(AddProduct), null)));
        }

        [Fact]
        public async Task ShouldFailToStartWhenARegisteredTypeDoesntImplementTheServicesMessageInterface()
        {
            var error = Assert.Single(await StartupErrorsAsync(typeof(AddProduct), typeof(ShipProduct)));

            Assert.Contains("'catalogue'", error);
            Assert.Contains("ShipProduct", error);
            Assert.Contains("ICatalogueMessage", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenARegisteredTypeIsMoreThanOneKindOfMessage()
        {
            var error = Assert.Single(await StartupErrorsAsync(typeof(AddOrUpdateProduct)));

            Assert.Contains("AddOrUpdateProduct", error);
            Assert.Contains("command", error);
            Assert.Contains("event", error);
            Assert.Contains("exactly one of ICommand, IEvent, IRequest or IResponse", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenARegisteredTypeIsNoKindOfMessage()
        {
            var error = Assert.Single(await StartupErrorsAsync(typeof(ProductNote)));

            Assert.Contains("ProductNote", error);
            Assert.Contains("exactly one of ICommand, IEvent, IRequest or IResponse", error);
        }

        [Fact]
        public async Task ShouldFailToStartWhenARegisteredTypeCantBeDeserialized()
        {
            var errors = await StartupErrorsAsync(typeof(ProductCommand), typeof(ReindexProducts));

            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, error => error.Contains("ProductCommand") && error.Contains("abstract"));
            Assert.Contains(errors, error => error.Contains("ReindexProducts") && error.Contains("public"));
        }

        [Fact]
        public async Task ShouldStartWhenEveryRegisteredTypeIsValid()
        {
            using var host = BuildHost(typeof(AddProduct), typeof(ProductAdded), typeof(GetPrice), typeof(Price));

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        private static ObvsServiceBuilder Configure(Action<ObvsServiceBuilder> configure)
        {
            ObvsServiceBuilder captured = null;
            new ServiceCollection().AddObvs(obvs => obvs.AddService<ICatalogueMessage>("catalogue", service =>
            {
                captured = service;
                configure(service);
            }));
            return captured;
        }

        private static async Task<IReadOnlyList<string>> StartupErrorsAsync(params Type[] messageTypes)
        {
            using var host = BuildHost(messageTypes);
            var exception = await Assert.ThrowsAsync<ObvsConfigurationException>(() => host.StartAsync(TestContext.Current.CancellationToken));
            return exception.Errors;
        }

        private static IHost BuildHost(params Type[] messageTypes)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<ICatalogueMessage>("catalogue", service => service
                .UseInMemory()
                .AddMessages(messageTypes)));
            return builder.Build();
        }

        // Creates a deserializer for any type it's given, and counts scans
        private sealed class TypedFactory : IMessageDeserializerFactory, IMessageTypeDeserializerFactory
        {
            public int Scans { get; private set; }

            public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
                where TMessage : class
                where TServiceMessage : class
            {
                Scans++;
                return Enumerable.Empty<IMessageDeserializer<TMessage>>();
            }

            public IMessageDeserializer<TMessage> Create<TMessage>(Type messageType) where TMessage : class
            {
                return new NamedDeserializer<TMessage>(messageType);
            }
        }

        // Only scans, finding the types it was given that pass the filter
        private sealed class ScanningFactory : IMessageDeserializerFactory
        {
            private readonly Type[] _found;

            public ScanningFactory(params Type[] found) => _found = found;

            public IEnumerable<IMessageDeserializer<TMessage>> Create<TMessage, TServiceMessage>(Func<Assembly, bool> assemblyFilter = null, Func<Type, bool> typeFilter = null)
                where TMessage : class
                where TServiceMessage : class
            {
                return _found
                    .Where(type => typeof(TMessage).IsAssignableFrom(type) && typeof(TServiceMessage).IsAssignableFrom(type))
                    .Where(type => (assemblyFilter?.Invoke(type.Assembly) ?? true) && (typeFilter?.Invoke(type) ?? true))
                    .Select(type => new NamedDeserializer<TMessage>(type))
                    .ToArray();
            }
        }

        private sealed class NamedDeserializer<TMessage> : IMessageDeserializer<TMessage> where TMessage : class
        {
            private readonly Type _type;

            public NamedDeserializer(Type type) => _type = type;

            public TMessage Deserialize(Stream source) => throw new NotSupportedException();

            public string GetTypeName() => _type.Name;
        }

        private sealed class NullSerializer : IMessageSerializer
        {
            public void Serialize(Stream destination, object message)
            {
            }
        }
    }
}
