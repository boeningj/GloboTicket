using System;

namespace GloboTicket.Services.ShoppingBasket.Entities
{
    public class Ticket
    {
        public Guid TicketId { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
    }
}