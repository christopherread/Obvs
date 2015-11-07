using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace Obvs.Monitoring
{
    public class ConsoleTimerMonitor<TMessage> : IMonitor<TMessage>
    {
        private readonly string _name;
        private readonly TimeSpan _period;
        private readonly IScheduler _scheduler;
        private readonly IDisposable _timer;

        private int _sent;
        private int _received;
        private TimeSpan _averageSendTime = TimeSpan.Zero;
        private TimeSpan _averageReceiveTime = TimeSpan.Zero;

        public ConsoleTimerMonitor(string name, TimeSpan period, IScheduler scheduler)
        {
            _name = name;
            _period = period;
            _scheduler = scheduler;
            _timer = StartTimer();
        }

        private IDisposable StartTimer()
        {
            return Observable.Timer(_period, _period, _scheduler)
                .ObserveOn(_scheduler)
                .Subscribe(_ =>
                {
                    try
                    {
                        DisplayMonitoring();
                        ResetCounters();
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                    }
                });
        }

        private void DisplayMonitoring()
        {
            Console.WriteLine("Monitor[{0}] SentTotal: {1}msg, SendRate: {2}msg/sec, SendAvg: {3}ms", _name, _sent,
                (_sent/_period.TotalSeconds).ToString("n1"), _averageSendTime.TotalMilliseconds.ToString("n0"));

            Console.WriteLine("Monitor[{0}] ReceivedTotal: {1}msg, ReceivedRate: {2}msg/sec, ReceivedAvg: {3}ms", _name, _received,
                (_received/_period.TotalSeconds).ToString("n1"), _averageReceiveTime.TotalMilliseconds.ToString("n0"));
        }

        private void ResetCounters()
        {
            _sent = 0;
            _received = 0;
            _averageSendTime = TimeSpan.Zero;
            _averageReceiveTime = TimeSpan.Zero;
        }

        public void MessageSent(TMessage message, TimeSpan elapsed)
        {
            _sent++;
            _averageSendTime = new TimeSpan(((_sent - 1)*_averageSendTime.Ticks + elapsed.Ticks)/_sent);
        }

        public void MessageReceived(TMessage message, TimeSpan elapsed)
        {
            _received++;
            _averageReceiveTime = new TimeSpan(((_sent - 1) * _averageReceiveTime.Ticks + elapsed.Ticks) / _sent);
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }
        }
    }
}