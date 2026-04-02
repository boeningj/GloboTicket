using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GloboTicket.Services.EventCatalog.Entities
{
    public class Venue
    {
        [Required]
        // Removed for Cosmos DB
        //public Guid VenueId { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        // Removed for Cosmos DB
        //public List<Event> Events { get; set; }
    }
}