using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Gateway.WebBff.Services
{
    public interface IOrderService
    {
        Task<List<OrderDto>> GetOrdersForUser(Guid userId);
        Task<OrderDto> GetOrderDetails(Guid orderId);
    }
}
