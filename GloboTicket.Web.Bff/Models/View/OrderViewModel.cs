using GloboTicket.Web.Models.Api;
using System.Collections.Generic;

using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Web.Models.View
{
    public class OrderViewModel
    {
        public IEnumerable<OrderDto> Orders { get; set; }
    }
}
