using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using Obvs.Configuration;
using Obvs.Kafka.Configuration;
using Obvs.Serialization.Json.Configuration;
using Obvs.Types;

namespace Obvs.Kafka.Tests
{
    [TestFixture]
    public class CompareWithActiveMQ
    {
        string _kafkaAddresses = "192.168.99.102";
        string _activeMqUri = "failover:(tcp://192.168.99.102:61616)";

         Random _rnd = new Random();
        private Stopwatch _sw;

        [SetUp]
        public void SetUp()
        {
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var consoleTarget = new ColoredConsoleTarget();
            // Step 3. Set target properties 
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} ${message}";

            // Uncomment to log to console.
            //config.AddTarget("console", consoleTarget);

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Trace, consoleTarget);
            config.LoggingRules.Add(rule1);

            // Step 5. Activate the configuration
            NLog.LogManager.Configuration = config;

            kafka4net.Logger.SetupNLog();
        }


        [Explicit]
        [Test]
        [TestCase(10000, 10)]
        public async Task TestServiceBusWithRemoteKafka(int count, int watchers)
        {
            var tasks = Enumerable.Range(0, watchers)
                .Select(i => StartWatcher(i, count))
                .ToArray();

            Thread.Sleep(5000);

            await SendCommands(count);

            await Task.WhenAll(tasks);
            
        }

        private async Task SendCommands(int count)
        {
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithKafkaEndpoints<ITestMessage1>()
                    .Named("Obvs.TestService")
                    .WithKafkaSourceConfiguration(new KafkaSourceConfiguration())
                    .WithKafkaProducerConfiguration(new KafkaProducerConfiguration())
                    .ConnectToKafka(_kafkaAddresses)
                    .SerializedAsJson()
                    .AsClient()
                //.UsingConsoleLogging()
                .Create();

            Stopwatch sw = Stopwatch.StartNew();

            var sendTasks = Enumerable.Range(0, count)
                .Select(i => serviceBus.SendAsync(new TestCommand { Id = i }));

            await Task.WhenAll(sendTasks);

            _sw = Stopwatch.StartNew();
            Console.WriteLine($"###$$$$### Sends: {sw.ElapsedMilliseconds}ms");

            ((IDisposable)serviceBus).Dispose();
        }

        private Task StartWatcher(int i, int count)
        {
            IServiceBus serviceBus = ServiceBus.Configure()
                .WithKafkaEndpoints<ITestMessage1>()
                    .Named("Obvs.TestService")
                    .ConnectToKafka(_kafkaAddresses)
                    .SerializedAsJson()
                    .AsServer()
                //.UsingConsoleLogging()
                .Create();

            double?[] times = new double?[count];
            long[] received = { 0 };


            var dis = serviceBus.Commands.OfType<TestCommand>().Subscribe(x =>
            {
                Interlocked.Increment(ref received[0]);
                var ms = (Stopwatch.GetTimestamp() - x.Ticks) / ((double) Stopwatch.Frequency / 1000);
                times[x.Id] = ms;
            });

            return Task.Run(() =>
            {
                SpinWait.SpinUntil(() => Interlocked.Read(ref received[0]) == count);

                Console.WriteLine($"******* Watcher {i}: Total {_sw.ElapsedMilliseconds}ms ({count} msgs), Min/Avg/Max (ms) = {times.Min(d=>d.Value):0}/{times.Average(d => d.Value):0}/{times.Max(d => d.Value):0}");

                dis.Dispose();
                ((IDisposable)serviceBus).Dispose();
            });
        }


        public interface ITestMessage1 : IMessage
        {
        }

        public interface ITestMessage2 : IMessage
        {
        }

        public class TestEvent : ITestMessage1, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public long Ticks { get; set; } = Stopwatch.GetTimestamp();

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class Test2Event : ITestMessage1, IEvent
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class Test2Command : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand2 : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestCommand3 : ITestMessage1, ICommand
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }
        }

        public class TestRequest : ITestMessage1, IRequest
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }

        public class TestResponse : ITestMessage1, IResponse
        {
            public int Id { get; set; }

            public override string ToString()
            {
                return string.Format("{0}[Id={1}]", GetType().Name, Id);
            }

            public string RequestId { get; set; }
            public string RequesterId { get; set; }
        }
    }
}
