using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using System.Net.Http;
using GloboTicket.Gateway.WebBff.Extensions;
using GloboTicket.Gateway.Shared.Order;
using GloboTicket.Gateway.WebBff.Url;

namespace GloboTicket.Gateway.WebBff.Services
{
    public class OrderService : IOrderService
    {
        private readonly HttpClient client;

        public OrderService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<List<OrderDto>> GetOrdersForUser(Guid userId)
        {
            var response = await client.GetAsync(OrderingOperations.GetOrdersForUser(userId));
            return await response.ReadContentAs<List<OrderDto>>();
        }

        public async Task<OrderDto> GetOrderDetails(Guid orderId)
        {
            var response = await client.GetAsync(OrderingOperations.GetOrderDetails(orderId));
            return await response.ReadContentAs<OrderDto>();
        }
    }
}
