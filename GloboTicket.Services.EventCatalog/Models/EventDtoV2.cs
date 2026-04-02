using System;
using System.Collections.Generic;

namespace GloboTicket.Services.EventCatalog.Models.V2
{
    public class EventDto
    {
        public Guid EventId { get; set; }
        public string Name { get; set; }
        public string Artist { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string VenueName { get; set; }
        public string VenueCity { get; set; }
        public string VenueCountry { get; set; }
        public List<TicketDto> Tickets { get; set; }
    }
}