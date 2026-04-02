using System;
using System.ComponentModel.DataAnnotations;

namespace GloboTicket.Gateway.Shared.Basket
{
    public class BasketLineForCreationDto
    {
        [Required]
        public Guid EventId { get; set; }
        [Required]
        public int TicketAmount { get; set; }
        [Required]
        public int Price { get; set; }
    }
}