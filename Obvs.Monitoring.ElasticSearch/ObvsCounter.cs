using System;
using Nest;

namespace Obvs.Monitoring.ElasticSearch
{
    [ElasticType(Name = "obvscounter")]
    public class ObvsCounter
    {
        public string Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime TimeStampUTC { get; set; }
        [ElasticProperty(OmitNorms = true, Index = FieldIndexOption.NotAnalyzed)]
        public string CounterName { get; set; }
        [ElasticProperty(OmitNorms = true, Index = FieldIndexOption.NotAnalyzed)]
        public string InstanceName { get; set; }
        [ElasticProperty(OmitNorms = true, Index = FieldIndexOption.NotAnalyzed)]
        public string MessageType { get; set; }
        public string Direction { get; set; }
        public int Count { get; set; }
        public double RatePerSec { get; set; }
        public double AvgElapsedMs { get; set; }
        public double MaxElapsedMs { get; set; }
        public double MinElapsedMs { get; set; }
        public double TotalElapsedMs { get; set; }
        public string UserName { get; set; }
        [ElasticProperty(OmitNorms = true, Index = FieldIndexOption.NotAnalyzed)]
        public string HostName { get; set; }
        [ElasticProperty(OmitNorms = true, Index = FieldIndexOption.NotAnalyzed)]
        public string ProcessName { get; set; }

        public ObvsCounter()
        {
            Id = Guid.NewGuid().ToString();
        } 
    }
}