using System;
using System.Collections.Generic;

namespace GloboTicket.Gateway.Shared.Order
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int OrderTotal { get; set; }
        public DateTime OrderPlaced { get; set; }
        public bool OrderPaid { get; set; }
        public List<OrderLineDto> OrderLines { get; set; }
        public string Message { get; set; }
    }
}