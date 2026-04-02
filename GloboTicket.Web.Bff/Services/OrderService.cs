using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Web.Services
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
            var response = await client.GetAsync($"/api/bffweb/order/user/{userId}");
            return await response.ReadContentAs<List<OrderDto>>();
        }

        public async Task<OrderDto> GetOrderDetails(Guid orderId)
        {
            var response = await client.GetAsync($"/api/bffweb/order/{orderId}");
            return await response.ReadContentAs<OrderDto>();
        }
    }
}
