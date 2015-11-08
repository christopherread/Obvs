using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Nest;

namespace Obvs.Monitoring.ElasticSearch
{
    public class ElasticSearchMonitor<TMessage> : IMonitor<TMessage>
    {
        static class Directions
        {
            public const string Send = "send";
            public const string Receive = "receive";
        }

        private readonly string _name;
        private readonly string _indexName;
        private readonly IList<Type> _types;
        private readonly ElasticClient _client;
        private readonly Subject<Tuple<TMessage, TimeSpan>> _queueSent = new Subject<Tuple<TMessage, TimeSpan>>(); 
        private readonly Subject<Tuple<TMessage, TimeSpan>> _queueReceived = new Subject<Tuple<TMessage, TimeSpan>>();
        private readonly TimeSpan _samplePeriod;
        private readonly string _messageTypeName;
        private readonly IDisposable _subscription;
        private readonly bool _typeCounters;
        private readonly string _userName;
        private readonly string _hostName;
        private readonly string _processName;

        public ElasticSearchMonitor(string name, IConnectionSettingsValues connectionSettings, string indexName, 
                                    IList<Type> types, IScheduler scheduler)
        {
            _messageTypeName = typeof(TMessage).Name;
            _name = name;
            _indexName = indexName;
            _types = types ?? new List<Type>();
            _typeCounters = _types.Any();
            _client = new ElasticClient(connectionSettings);
            _userName = Environment.UserName;
            _hostName = Dns.GetHostName();
            _processName = Process.GetCurrentProcess().ProcessName;

            _samplePeriod = TimeSpan.FromSeconds(1);
            var bufferPeriod = TimeSpan.FromSeconds(5);
           

            _subscription =
                _queueSent.Buffer(_samplePeriod, scheduler).SelectMany(CreateSent)
                    .Merge(_queueReceived.Buffer(_samplePeriod, scheduler).SelectMany(CreateReceived))
                    .Buffer(bufferPeriod, scheduler)
                    .ObserveOn(scheduler)
                    .Subscribe(async items => await Save(items));
        }

        private async Task Save(IEnumerable<ObvsCounter> items)
        {
            try
            {
                var indexName = _indexName;
                var response = await _client.IndexManyAsync(items, indexName);
                if (!response.IsValid && response.ServerError != null)
                {
                    Debug.WriteLine(response.ServerError.Error);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        private IList<ObvsCounter> CreateSent(IList<Tuple<TMessage, TimeSpan>> items)
        {
            return Create(items, Directions.Send);
        }

        private IList<ObvsCounter> CreateReceived(IList<Tuple<TMessage, TimeSpan>> items)
        {
            return Create(items, Directions.Receive);
        }

        private IList<ObvsCounter> Create(IList<Tuple<TMessage, TimeSpan>> items, string direction)
        {
            DateTime timeStamp = DateTime.Now;
            
            var list = new List<ObvsCounter>
            {
                Create(direction, items.Count, items, _messageTypeName, timeStamp)
            };

            if (_typeCounters)
            {
                list.AddRange(
                    items.GroupBy(t => t.Item1.GetType())
                    .Where(g => _types.Contains(g.Key))
                    .Select(g => Create(direction, g.Count(), g, g.Key.Name, timeStamp))
                    .ToArray());
            }

            return list;
        }

        private ObvsCounter Create(string direction, int count, IEnumerable<Tuple<TMessage, TimeSpan>> items, string messageType, DateTime timeStamp)
        {
            return new ObvsCounter
            {
                Direction = direction,
                Count = count,
                RatePerSec = count / _samplePeriod.TotalSeconds,
                AvgElapsedMs = items.Average(t => t.Item2.TotalMilliseconds),
                Name = _name,
                MessageType = messageType,
                TimeStamp = timeStamp,
                TimeStampUTC = timeStamp.ToUniversalTime(),
                UserName = _userName,
                HostName = _hostName,
                ProcessName = _processName
            };
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _queueSent.Dispose();
            _queueReceived.Dispose();
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
            _queueSent.OnNext(Tuple.Create(message, elapsed));
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            _queueReceived.OnNext(Tuple.Create(message, elapsed));
        }
    }
}
