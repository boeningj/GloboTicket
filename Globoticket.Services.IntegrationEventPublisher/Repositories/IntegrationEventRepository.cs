using Globoticket.Services.IntegrationEventPublisher.DbContexts;
using Globoticket.Services.IntegrationEventPublisher.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Globoticket.Services.IntegrationEventPublisher.Repositories
{
    public class IntegrationEventRepository : IIntegrationEventRepository
    {
        //private readonly DbContextOptions<IntegrationEventsDbContext> dbContextOptions;
        private readonly IntegrationEventsDbContext _dbContext;

        // public IntegrationEventRepository(DbContextOptions<IntegrationEventsDbContext> dbContextOptions)
        // {
        //     this.dbContextOptions = dbContextOptions;
        // }

        public IntegrationEventRepository(IntegrationEventsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<IntegrationEventLogEntry>> GetUnpublishedEvents()
        {
            //await using var _dbContext = new IntegrationEventsDbContext(dbContextOptions);
            return await _dbContext.IntegrationEventLogEntries
                .Where(e => e.State == "Pending").ToListAsync();
        }

        // For SQL Server DB
        // public async Task UpdateIntegrationEventLogEntryState(IntegrationEventLogEntry entry, string state)
        // {
        //     //await using var _dbContext = new IntegrationEventsDbContext(dbContextOptions);
        //     var entryInDatabase = await _dbContext.IntegrationEventLogEntries
        //             .Where(e => e.IntegrationEventLogId == entry.IntegrationEventLogId).FirstOrDefaultAsync();
        //     // could perform optimistic concurrency check to ensure another process hasn't changed the state since last retrieved
        //     entryInDatabase.State = state;
        //     await _dbContext.SaveChangesAsync();

        // }

        // Mark a tracked entity as in-process or failed, but do NOT save yet
        public void UpdateIntegrationEventLogEntryState(IntegrationEventLogEntry entry, string state)
        {
            if (entry != null)
            {
                entry.State = state;
                // no SaveChangesAsync here
            }
        }

        // Commit all changes at once (batch save) = Cosmos DB optimized
        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }
    }
}
