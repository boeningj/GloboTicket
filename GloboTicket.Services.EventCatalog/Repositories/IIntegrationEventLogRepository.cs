using GloboTicket.Services.EventCatalog.Entities;

public interface IIntegrationEventLogRepository
{
    void AddEventLogEntry(IntegrationEventLog logEntry);
    void UpdateEventLogEntry(IntegrationEventLog logEntry);
}