using GloboTicket.Services.ShoppingBasket.DbContexts;
using GloboTicket.Services.ShoppingBasket.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class SqliteBasketLinesRepository : IBasketLinesRepository
    {
        private readonly ShoppingBasketDbContext context;

        public SqliteBasketLinesRepository(ShoppingBasketDbContext context)
        {
            this.context = context;
        }

        public async Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId)
        {
            return await context.BasketLines.Include(bl => bl.Event)
                .Where(b => b.BasketId == basketId).ToListAsync();
        }

        public async Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId)
        {
            // return await context.BasketLines.Include(bl => bl.Event)
            //     .Where(b => b.BasketLineId == basketLineId).FirstOrDefaultAsync();
            return await context.BasketLines.Include(bl => bl.Event)
            .Where(b => b.BasketId == basketId && b.BasketLineId == basketLineId)
            .FirstOrDefaultAsync();
        }

        public async Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine)
        {
            var existing = await context.BasketLines
                .FirstOrDefaultAsync(bl => bl.BasketId == basketId && bl.EventId == basketLine.EventId);

            if (existing != null)
            {
                existing.TicketAmount += basketLine.TicketAmount;
                context.BasketLines.Update(existing);
                return existing;
            }

            basketLine.BasketId = basketId;
            context.BasketLines.Add(basketLine);
            return basketLine;
        }

        public Task UpdateBasketLine(BasketLine basketLine)
        {
            context.BasketLines.Update(basketLine);
            return Task.CompletedTask;
        }

        public Task RemoveBasketLine(BasketLine basketLine)
        {
            context.BasketLines.Remove(basketLine);
            return Task.CompletedTask;
        }

        public async Task<bool> SaveChanges()
        {
            return (await context.SaveChangesAsync()) > 0;
        }
    }
}