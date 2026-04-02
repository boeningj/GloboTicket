using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace GloboTicket.Services.EventCatalog.Entities
{
    public class Event
    {
        [Required]
        public Guid EventId { get; set; }
        public string Name { get; set; }
        // Obsolete - to be removed - use Tickets collection instead
        public int Price { get; set; }
        public string Artist { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        // Removed for Cosmos DB
        // public Guid CategoryId { get; set; }
        // public Category Category { get; set; }

        // public Guid VenueId { get; set; }
        // public Venue Venue { get; set; }

        // CategoryId is now the partitionKey used in Cosmos Db
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string VenueName { get; set; }
        public string VenueCity { get; set; }
        public string VenueCountry { get; set; }
        //v2 addition
        public List<Ticket> Tickets { get; set; } = new ();
    }
}
