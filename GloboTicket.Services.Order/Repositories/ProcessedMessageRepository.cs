using GloboTicket.Services.Ordering.DbContexts;
using GloboTicket.Services.Ordering.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace GloboTicket.Services.Ordering.Repositories
{
    public class ProcessedMessageRepository : IProcessedMessageRepository
    {
        private readonly DbContextOptions<OrderDbContext> _dbContextOptions;

        public ProcessedMessageRepository(DbContextOptions<OrderDbContext> dbContextOptions)
        {
            _dbContextOptions = dbContextOptions;
        }

        public async Task<bool> HasBeenProcessedAsync(string messageId)
        {
            await using var db = new OrderDbContext(_dbContextOptions);
            return await db.ProcessedMessages.AnyAsync(pm => pm.MessageId == messageId);
        }

        public async Task MarkAsProcessedAsync(string messageId)
        {
            await using var db = new OrderDbContext(_dbContextOptions);
            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                ProcessedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}