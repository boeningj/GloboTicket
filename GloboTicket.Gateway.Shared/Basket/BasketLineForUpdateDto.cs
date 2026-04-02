using System;
using System.ComponentModel.DataAnnotations;

namespace GloboTicket.Gateway.Shared.Basket
{
    public class BasketLineForUpdateDto
    {
        [Required]
        public Guid LineId { get; set; }
        [Required]
        public int TicketAmount { get; set; }
    }
}