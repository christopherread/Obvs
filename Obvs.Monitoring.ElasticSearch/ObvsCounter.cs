using System;

namespace Obvs.Monitoring.ElasticSearch
{
    public class ObvsCounter
    {
        public string Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime TimeStampUTC { get; set; }
        public double AvgElapsedMs { get; set; }   
        public string MessageType { get; set; }
        public string Name { get; set; }
        public string Direction { get; set; }
        public double RatePerSec { get; set; }
        public int Count { get; set; }
        public string UserName { get; set; }
        public string HostName { get; set; }
        public string ProcessName { get; set; }

        public ObvsCounter()
        {
            Id = Guid.NewGuid().ToString();
        } 
    }
}