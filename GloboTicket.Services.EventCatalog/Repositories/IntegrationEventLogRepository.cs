using GloboTicket.Services.EventCatalog.DbContexts;
using GloboTicket.Services.EventCatalog.Entities;

namespace GloboTicket.Services.EventCatalog.Repositories
{
    public class IntegrationEventLogRepository : IIntegrationEventLogRepository
    {
        private readonly EventCatalogCosmosDbContext _dbContext;

        public IntegrationEventLogRepository(EventCatalogCosmosDbContext dbContext)
        {
            this._dbContext = dbContext;
        }

        public void AddEventLogEntry(IntegrationEventLog logEntry)
        {
            _dbContext.IntegrationEventLogs.Add(logEntry);
        }

        public void UpdateEventLogEntry(IntegrationEventLog logEntry)
        {
            _dbContext.IntegrationEventLogs.Update(logEntry);
        }
    }
}