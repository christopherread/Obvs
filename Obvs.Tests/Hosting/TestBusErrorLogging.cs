using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obvs.Configuration;
using Obvs.Hosting;
using Obvs.Tests.Hosting.Handlers;
using Obvs.Transports;
using Obvs.Types;
using Xunit;

namespace Obvs.Tests.Hosting
{
    // Errors a transport reports on the bus's Exceptions stream are logged too, so they're seen without subscribing to it
    public class TestBusErrorLogging
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ShouldLogErrorsTheBusReports()
        {
            var logs = new ListLoggerProvider();
            var failure = new InvalidOperationException("The orders queue was deleted");
            using var host = BuildHost(logs, FailingOnce<ICommand>(failure));

            await host.StartAsync(Ct);

            var entry = await WaitForAsync(() => logs.Entries.FirstOrDefault(e => e.Exception != null && e.Exception.ToString().Contains(failure.Message)));
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Obvs.Bus", entry.Category);
        }

        [Fact]
        public async Task ShouldStopLoggingWhenTheHostStops()
        {
            var logs = new ListLoggerProvider();
            var commands = new System.Reactive.Subjects.Subject<ICommand>();
            using var host = BuildHost(logs, commands);
            await host.StartAsync(Ct);
            await host.StopAsync(Ct);

            commands.OnError(new InvalidOperationException("After stopping"));

            Assert.DoesNotContain(logs.Entries, e => e.Exception != null && e.Exception.ToString().Contains("After stopping"));
        }

        // Errors on its first subscription, then stays quiet, as a transport whose queue was deleted and recreated
        private static IObservable<T> FailingOnce<T>(Exception failure)
        {
            var subscriptions = 0;
            return Observable.Defer(() => Interlocked.Increment(ref subscriptions) == 1 ? Observable.Throw<T>(failure) : Observable.Never<T>());
        }

        private static IHost BuildHost(ListLoggerProvider logs, IObservable<ICommand> commands)
        {
            var endpoint = A.Fake<IServiceEndpoint<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => endpoint.CanHandle(A<IMessage>._)).Returns(true);
            A.CallTo(() => endpoint.Commands).Returns(commands);
            A.CallTo(() => endpoint.Requests).Returns(Observable.Never<IRequest>());
            var endpointClient = A.Fake<IServiceEndpointClient<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => endpointClient.Events).Returns(Observable.Never<IEvent>());
            var provider = A.Fake<IServiceEndpointProvider<IMessage, ICommand, IEvent, IRequest, IResponse>>();
            A.CallTo(() => provider.CreateEndpoint()).Returns(endpoint);
            A.CallTo(() => provider.CreateEndpointClient()).Returns(endpointClient);

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services
                .AddLogging(logging => logging.AddProvider(logs))
                .AddObvs(obvs => obvs.AddService<IOrderMessage>("orders", service => service
                    .UseEndpointProvider(_ => provider)
                    .HandleCommand<PlaceOrder>((command, context, ct) => Task.CompletedTask)));
            return builder.Build();
        }

        private static async Task<T> WaitForAsync<T>(Func<T> find) where T : class
        {
            var deadline = DateTime.UtcNow + Timeout;
            T found;
            while ((found = find()) == null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20, Ct);
            }
            return found ?? throw new TimeoutException("Nothing was logged in time.");
        }
    }
}
