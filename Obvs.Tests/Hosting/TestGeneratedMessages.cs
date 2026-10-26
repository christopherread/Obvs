using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obvs.Hosting;
using Obvs.Testing;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting
{
    public interface IInventoryMessage : IMessage { }

    public abstract class InventoryCommand : IInventoryMessage, ICommand { }

    public class ReserveStock : InventoryCommand { }

    public class StockReserved : IInventoryMessage, IEvent { }

    public class GetStockLevel : IInventoryMessage, IRequest
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    public class StockLevel : IInventoryMessage, IResponse
    {
        public string RequestId { get; set; }
        public string RequesterId { get; set; }
    }

    // Obvs's source generator adds Types, listing the IInventoryMessage classes above
    [ObvsMessages(typeof(IInventoryMessage))]
    public partial class InventoryMessages { }

    public class TestGeneratedMessages
    {
        [Fact]
        public void ShouldRegisterTheMessagesTheGeneratorFound()
        {
            ObvsServiceBuilder inventory = null;
            new ServiceCollection().AddObvs(obvs => obvs.AddService<IInventoryMessage>("inventory", service =>
                inventory = service.AddMessages(InventoryMessages.Types)));

            Assert.Equal(
                new[] { typeof(GetStockLevel), typeof(ReserveStock), typeof(StockLevel), typeof(StockReserved) },
                inventory.Messages.OrderBy(type => type.Name));
        }

        [Fact]
        public async Task ShouldStartWithTheGeneratedRegistration()
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddObvs(obvs => obvs.AddService<IInventoryMessage>("inventory", service => service
                .UseInMemory()
                .AddMessages(InventoryMessages.Types)));
            using var host = builder.Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
