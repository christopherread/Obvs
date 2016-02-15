using System;
using Nest;

namespace Obvs.Monitoring.ElasticSearch
{
    public class ObvsCounter
    {
        public string Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime TimeStampUTC { get; set; }
        public string CounterName { get; set; }
        public string InstanceName { get; set; }
        public string MessageType { get; set; }
        public string Direction { get; set; }
        public int Count { get; set; }
        public double RatePerSec { get; set; }
        public double AvgElapsedMs { get; set; }
        public double MaxElapsedMs { get; set; }
        public double MinElapsedMs { get; set; }
        public double TotalElapsedMs { get; set; }
        public string UserName { get; set; }
        public string HostName { get; set; }
        public string ProcessName { get; set; }

        public ObvsCounter()
        {
            Id = Guid.NewGuid().ToString();
        } 
    }
}