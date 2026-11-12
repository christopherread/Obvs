using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Obvs.Hosting;
using Xunit;

namespace Obvs.Testing.Tests
{
    // A committed copy of a bus's contract, which a test keeps up to date and fails on a breaking change
    public class TestObvsContractSnapshot : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "obvs-contract-" + Guid.NewGuid().ToString("N"), "orders.asyncapi.json");

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public void Dispose()
        {
            if (Directory.Exists(Path.GetDirectoryName(_path)))
            {
                Directory.Delete(Path.GetDirectoryName(_path), recursive: true);
            }
        }

        [Fact]
        public async Task ShouldWriteTheContractTheFirstTime()
        {
            await using var harness = await StartAsync(typeof(PlaceOrder), typeof(OrderPlaced));

            ObvsContractSnapshot.Verify(harness.Services, _path);

            Assert.Equal(harness.Services.GetRequiredService<ObvsContracts>().Describe(), File.ReadAllText(_path));
        }

        [Fact]
        public async Task ShouldPassWhenTheContractIsUnchanged()
        {
            await using var harness = await StartAsync(typeof(PlaceOrder), typeof(OrderPlaced));
            ObvsContractSnapshot.Verify(harness.Services, _path);
            var written = File.GetLastWriteTimeUtc(_path);

            ObvsContractSnapshot.Verify(harness.Services, _path);

            Assert.Equal(written, File.GetLastWriteTimeUtc(_path));
        }

        [Fact]
        public async Task ShouldUpdateTheContractWhenItOnlyGrows()
        {
            await using (var before = await StartAsync(typeof(PlaceOrder)))
            {
                ObvsContractSnapshot.Verify(before.Services, _path);
            }
            await using var after = await StartAsync(typeof(PlaceOrder), typeof(OrderPlaced));

            ObvsContractSnapshot.Verify(after.Services, _path);

            Assert.Contains("OrderPlaced", File.ReadAllText(_path));
        }

        [Fact]
        public async Task ShouldFailOnABreakingChangeAndKeepTheOldContract()
        {
            await using (var before = await StartAsync(typeof(PlaceOrder), typeof(OrderPlaced)))
            {
                ObvsContractSnapshot.Verify(before.Services, _path);
            }
            var committed = File.ReadAllText(_path);
            await using var after = await StartAsync(typeof(PlaceOrder));

            var exception = Assert.Throws<ObvsContractException>(() => ObvsContractSnapshot.Verify(after.Services, _path));

            Assert.Contains("OrderPlaced was removed from channel orders.events", exception.Message);
            Assert.Contains(_path, exception.Message);
            Assert.Equal(committed, File.ReadAllText(_path));
        }

        private static Task<ObvsTestHarness> StartAsync(params Type[] messages)
        {
            return ObvsTestHarness.StartAsync(services => services.AddObvs(obvs => obvs
                .AddService<IOrderMessage>("orders", service => service.UseInMemory().AddMessages(messages))),
                cancellationToken: Ct);
        }
    }
}
