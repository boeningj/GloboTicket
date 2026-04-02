using System;

namespace GloboTicket.Services.Ordering.Entities
{
    public class ProcessedMessage
    {
        public string MessageId { get; set; } = default!;
        public DateTime ProcessedAt { get; set; }
    }
}