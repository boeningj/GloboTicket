using GloboTicket.Services.EventCatalog.DbContexts;
using GloboTicket.Services.EventCatalog.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GloboTicket.Services.EventCatalog.Repositories
{
    public class EventRepository : IEventRepository
    {
        //private readonly EventCatalogDbContext _eventCatalogDbContext;

        private readonly EventCatalogCosmosDbContext _eventCatalogCosmosDbContext;

        // public EventRepository(EventCatalogDbContext eventCatalogDbContext)
        // {
        //     _eventCatalogDbContext = eventCatalogDbContext;
        // }

        public EventRepository(EventCatalogCosmosDbContext eventCatalogCosmosDbContext)
        {
            _eventCatalogCosmosDbContext = eventCatalogCosmosDbContext;
        }

        // public async Task<IEnumerable<Event>> GetEvents(Guid categoryId)
        // {
        //     return await _eventCatalogDbContext.Events
        //         .Include(x => x.Category)
        //         .Where(x => (x.CategoryId == categoryId || categoryId == Guid.Empty))
        //         .Include(x => x.Venue)
        //         .ToListAsync();
        // }

        // public async Task<Event> GetEventById(Guid eventId)
        // {
        //     return await _eventCatalogDbContext.Events
        //         .Include(x => x.Category)
        //         .Include(x => x.Venue)
        //         .Where(x => x.EventId == eventId).FirstOrDefaultAsync();
        // }

        // public async Task<bool> SaveChanges()
        // {
        //     return (await _eventCatalogDbContext.SaveChangesAsync() > 0);
        // }

        public async Task<IEnumerable<Event>> GetEvents(Guid categoryId)
        {
            /*
            return await _eventCatalogCosmosDbContext.Events
                //.Include(x => x.Category)
                .Where(x => (Guid.Parse(x.CategoryId) == categoryId || categoryId == Guid.Empty))
                //.Include(x => x.Venue)
                .ToListAsync();
                */

            IQueryable<Event> query = _eventCatalogCosmosDbContext.Events;

            if (categoryId != Guid.Empty)
            {
                query = query.Where(x => x.CategoryId == categoryId.ToString());
            }

            return await query.ToListAsync();
        }

        public async Task<Event> GetEventById(Guid eventId)
        {
            return await _eventCatalogCosmosDbContext.Events
                //.Include(x => x.Category)
                //.Include(x => x.Venue)
                .Where(x => x.EventId == eventId).FirstOrDefaultAsync();
        }
        
        public async Task<bool> SaveChanges()
        {
            return (await _eventCatalogCosmosDbContext.SaveChangesAsync() > 0);
        }
    }
}
