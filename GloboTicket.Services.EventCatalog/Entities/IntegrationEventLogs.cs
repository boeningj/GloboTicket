using System;

namespace GloboTicket.Services.EventCatalog.Entities
{
    public class IntegrationEventLog
    {
        public Guid IntegrationEventLogId { get; set; }
        public string IntegrationEventType { get; set; }
        public string ServiceBusTopicName { get; set; }
        public string IntegrationEventBody { get; set; }
        public string State { get; set; }
        public string PartitionKey { get; set; } = "IntegrationEventPublisher";
    }
}