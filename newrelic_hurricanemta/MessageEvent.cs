using System;

namespace newrelic_hurricanemta
{
    public partial class InsightEvent
    {
        public double timestamp { get; set; }
        public string eventType { get; set; }
    }

    public class MessageEvent : InsightEvent
    {
        public string outcome { get; set; }
        public string instance { get; set; }
        public string groupId { get; set; }
        public string projectId { get; set; }
        public string domain { get; set; }
        public int size { get; set; }
        public string failureCode { get; set; }
    }
}
