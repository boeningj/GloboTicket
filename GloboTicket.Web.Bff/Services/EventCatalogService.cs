using GloboTicket.Web.Extensions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Event;

namespace GloboTicket.Web.Services
{
    public class EventCatalogService : IEventCatalogService
    {
        private readonly HttpClient client;

        //Regular calls, replaced the microservice calls but now through the gateway

        #region Regular calls through Gateway

        public EventCatalogService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<IEnumerable<EventDto>> GetAll()
        {
            var response = await client.GetAsync("/api/bffweb/events");
            return await response.ReadContentAs<List<EventDto>>();
        }

        public async Task<IEnumerable<EventDto>> GetByCategoryId(Guid categoryid)
        {
            var response = await client.GetAsync($"/api/bffweb/events/{categoryid}");
            return await response.ReadContentAs<List<EventDto>>();
        }

        public async Task<EventDto> GetEvent(Guid id)
        {
            var response = await client.GetAsync($"/api/bffweb/events/event/{id}");
            return await response.ReadContentAs<EventDto>();
        }

        public async Task<IEnumerable<CategoryDto>> GetCategories()
        {
            var response = await client.GetAsync("/api/bffweb/events/categories");
            return await response.ReadContentAs<List<CategoryDto>>();
        }

        #endregion

        //Calls optimized for use through the gateway

        #region Gateway calls

        public async Task<CatalogBrowseDto> GetCatalogBrowse(Guid categoryId, Guid basketId)
        {
            var response = await client.GetAsync($"/api/bffweb/events/catalogbrowse/{categoryId}/basket/{basketId}");
            return await response.ReadContentAs<CatalogBrowseDto>();
        }

        #endregion
    }
}
