using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GloboTicket.Web.Services
{
    // -----------------------------------------------------------------------------
    // IMPORTANT: HttpClient BaseAddress + relative URI behavior
    //
    // Key rule:
    //   Relative path  → appended to BaseAddress
    //   Absolute path  → replaces BaseAddress path
    //
    // Example:
    //   BaseAddress = https://host/prefix/
    //
    //   GetAsync("api/x") → https://host/prefix/api/x        correct
    //   GetAsync("/api/x") → https://host/api/x              prefix LOST
    //
    // WHY THIS MATTERS:
    // Our services use BaseAddress prefixes like:  https://localhost:5051/order/
    //
    // Using a leading '/' will bypass the gateway prefix and break routing.
    //
    // ✅ ALWAYS use relative paths without leading '/':
    //     "api/order/..."
    // ❌ NEVER use:
    //     "/api/order/..."
    //
    // This bug already occurred once — do not remove this rule.
    // -----------------------------------------------------------------------------
    public class OrderService: IOrderService
    {
        private readonly HttpClient client;

        public OrderService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<List<Order>> GetOrdersForUser(Guid userId)
        {
            var response = await client.GetAsync($"api/order/user/{userId}");
            return await response.ReadContentAs<List<Order>>();
        }

        public async Task<Order> GetOrderDetails(Guid orderId)
        {
            var response = await client.GetAsync($"api/order/{orderId}");
            return await response.ReadContentAs<Order>();
        }
    }
}
