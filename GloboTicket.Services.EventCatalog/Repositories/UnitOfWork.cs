using System.Threading.Tasks;
using GloboTicket.Services.EventCatalog.DbContexts;

namespace GloboTicket.Services.EventCatalog.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly EventCatalogCosmosDbContext _dbContext;
        private IIntegrationEventLogRepository _integrationEventLogRepository;
        private IEventRepository _eventRepository;

        public UnitOfWork(EventCatalogCosmosDbContext databaseContext)
        { _dbContext = databaseContext; }

        public IEventRepository EventRepository
        {
            get { return _eventRepository = _eventRepository ?? new EventRepository(_dbContext); }
        }

        public IIntegrationEventLogRepository IntegrationEventLogRepository
        {
            get { return _integrationEventLogRepository = _integrationEventLogRepository ?? new IntegrationEventLogRepository(_dbContext); }
        }

        // public void Commit()
        // { _dbContext.SaveChanges(); }

        // public void Rollback()
        // { _dbContext.Dispose(); }

        public async Task CommitAsync()
        {
            await _dbContext.SaveChangesAsync();
        }

        public void Rollback()
        {
            _dbContext.ChangeTracker.Clear();
        }
    }
}