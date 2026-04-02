using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Web.Services
{
    public interface IOrderService
    {
        Task<List<OrderDto>> GetOrdersForUser(Guid userId);
        Task<OrderDto> GetOrderDetails(Guid orderId);
    }
}
