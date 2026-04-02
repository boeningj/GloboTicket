using System.Threading.Tasks;

namespace GloboTicket.Services.EventCatalog.Repositories
{
    public interface IUnitOfWork
    {
        IIntegrationEventLogRepository IntegrationEventLogRepository { get; }
        IEventRepository EventRepository { get; }
        //void Commit();
        Task CommitAsync();
        void Rollback();
    }
}