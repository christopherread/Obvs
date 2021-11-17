﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

        private readonly string _instanceName;
        private readonly string _counterName;
        private readonly string _indexPrefix;
        private readonly IList<Type> _types;
        private readonly IElasticClient _client;
        private readonly Subject<Tuple<TMessage, TimeSpan>> _queueSent = new Subject<Tuple<TMessage, TimeSpan>>(); 
        private readonly Subject<Tuple<TMessage, TimeSpan>> _queueReceived = new Subject<Tuple<TMessage, TimeSpan>>();
        private readonly TimeSpan _samplePeriod;
        private readonly string _messageTypeName;
        private readonly IDisposable _subscription;
        private readonly bool _typeCounters;
        private readonly string _userName;
        private readonly string _hostName;
        private readonly string _processName;
        private readonly IList<ObvsCounter> _emptyList = new List<ObvsCounter>();

        public ElasticSearchMonitor(string instanceName, string counterName, string indexPrefix, IList<Type> types, TimeSpan samplePeriod, IScheduler scheduler, IElasticClient client)
        {
            _messageTypeName = typeof(TMessage).Name;
            _instanceName = instanceName;
            _counterName = counterName;
            _indexPrefix = indexPrefix;
            _samplePeriod = samplePeriod;
            _client = client;
            _types = types ?? new List<Type>();
            _typeCounters = _types.Any();
            _userName = Environment.UserName;
            _hostName = Dns.GetHostName();
            _processName = Process.GetCurrentProcess().ProcessName;

            _subscription =
                _queueSent.Buffer(_samplePeriod, scheduler)
                          .Where(tuples => tuples.Any())
                          .SelectMany(CreateSent)
                    .Merge(_queueReceived.Buffer(_samplePeriod, scheduler)
                                         .Where(tuples => tuples.Any())
                                         .SelectMany(CreateReceived))
                    .Buffer(TimeSpan.FromSeconds(5), scheduler)
                    .Where(counters => counters.Any())
                    .ObserveOn(scheduler)
                    .Subscribe(Save);
        }

        private void Save(IList<ObvsCounter> items)
        {
            try
            {
                if (items.Any())
                {
                    string indexName = string.Format("{0}-{1}", _indexPrefix, DateTime.Today.ToString("yyyy.MM.dd"));
                    var response = _client.IndexMany(items, indexName);
                    if (!response.IsValid && response.ServerError != null)
                    {
                        Console.WriteLine(response.ServerError.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
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
            if (items.Any())
            {
                DateTime timeStamp = DateTime.Now;

                var list = new List<ObvsCounter>
                {
                    Create(direction, items, _messageTypeName, timeStamp)
                };

                if (_typeCounters)
                {
                    list.AddRange(
                        items.GroupBy(t => t.Item1.GetType())
                            .Where(g => _types.Contains(g.Key))
                            .Select(g => Create(direction, g.ToArray(), g.Key.Name, timeStamp))
                            .ToArray());
                }

                return list;
            }
            return _emptyList;
        }

        private ObvsCounter Create(string direction, IList<Tuple<TMessage, TimeSpan>> items, string messageType, DateTime timeStamp)
        {
            var count = items.Count;
            double max = 0;
            double min = 0;
            double total = 0;
            foreach (double milliseconds in items.Select(item => item.Item2.TotalMilliseconds))
            {
                max = Math.Max(milliseconds, max);
                min = Math.Min(milliseconds, min);
                total += milliseconds;
            }

            return new ObvsCounter
            {
                InstanceName = _instanceName,
                CounterName = _counterName,
                Direction = direction,
                MessageType = messageType,
                Count = count,
                RatePerSec = count / _samplePeriod.TotalSeconds,
                AvgElapsedMs = total / count,
                MaxElapsedMs = max,
                MinElapsedMs = min,
                TotalElapsedMs = total,
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
