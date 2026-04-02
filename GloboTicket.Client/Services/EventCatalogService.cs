using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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
    public class EventCatalogService : IEventCatalogService
    {
        private readonly HttpClient client;
        private readonly ILogger<EventCatalogService> logger;

        public EventCatalogService(HttpClient client, ILogger<EventCatalogService> logger)
        {
            this.client = client;
            this.logger = logger;
        }

        public async Task<IEnumerable<Event>> GetAll()
        {
            var response = await client.GetAsync("api/events");
            return await response.ReadContentAs<List<Event>>();
        }

        public async Task<IEnumerable<Event>> GetByCategoryId(Guid categoryid)
        {
            var response = await client.GetAsync($"api/events/?categoryId={categoryid}");
            return await response.ReadContentAs<List<Event>>();
        }

        public async Task<Event> GetEvent(Guid id)
        {
            var response = await client.GetAsync($"api/events/{id}");
            return await response.ReadContentAs<Event>();
        }

        public async Task<IEnumerable<Category>> GetCategories()
        {
            var response = await client.GetAsync("api/categories");
            return await response.ReadContentAs<List<Category>>();
        }

        // public async Task<PriceUpdate> UpdatePrice(PriceUpdate priceUpdate)
        // {
        //     var response = await client.PostAsJson($"api/events/eventpriceupdate", priceUpdate);
        //     return await response.ReadContentAs<PriceUpdate>();
        // }

        public async Task<EventUpdate> UpdateEvent(EventUpdate eventUpdate)
        {
            var response = await client.PostAsJson($"api/events/eventupdate", eventUpdate);
            return await response.ReadContentAs<EventUpdate>();
        }
    }
}
