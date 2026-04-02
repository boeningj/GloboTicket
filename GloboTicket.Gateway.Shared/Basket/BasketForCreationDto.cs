using System;
using System.ComponentModel.DataAnnotations;

namespace GloboTicket.Gateway.Shared.Basket
{
    public class BasketForCreationDto
    {
        [Required]
        public Guid UserId { get; set; }
    }
}