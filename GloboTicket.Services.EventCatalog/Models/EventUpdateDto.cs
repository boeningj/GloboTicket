using System;

namespace GloboTicket.Services.EventCatalog.Models
{
    public class EventUpdateDto
    {
        public Guid EventId { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public int Price { get; set; }
        public string Message { get; set; }
    }
}