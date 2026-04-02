using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Event;

namespace GloboTicket.Web.Services
{
    public interface IEventCatalogService
    {
        Task<IEnumerable<EventDto>> GetAll();
        Task<IEnumerable<EventDto>> GetByCategoryId(Guid categoryid);
        Task<EventDto> GetEvent(Guid id);
        Task<IEnumerable<CategoryDto>> GetCategories();
        Task<CatalogBrowseDto> GetCatalogBrowse(Guid basketId, Guid categoryId);
    }
}
