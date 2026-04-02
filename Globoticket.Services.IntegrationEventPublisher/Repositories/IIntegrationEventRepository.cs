using Globoticket.Services.IntegrationEventPublisher.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Globoticket.Services.IntegrationEventPublisher.Repositories
{
    public interface IIntegrationEventRepository
    {
        Task<IEnumerable<IntegrationEventLogEntry>> GetUnpublishedEvents();
        void UpdateIntegrationEventLogEntryState(IntegrationEventLogEntry entry, string state);
        Task SaveChangesAsync();
    }
}
