using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Repositories
{
    public interface IProcessedMessageRepository
    {
        Task<bool> HasBeenProcessedAsync(string messageId);
        Task MarkAsProcessedAsync(string messageId);
    }
}