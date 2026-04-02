using System;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.DbContexts;
using GloboTicket.Services.ShoppingBasket.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class SqliteBasketRepository : IBasketRepository
    {
        private readonly ShoppingBasketDbContext context;

        public SqliteBasketRepository(ShoppingBasketDbContext context)
        {
            this.context = context;
        }

        public async Task<bool> BasketExists(Guid basketId)
        {
            return await context.Baskets.AnyAsync(b => b.BasketId == basketId);
        }

        public async Task<Basket> GetBasketById(Guid basketId)
        {
            // return await context.Baskets
            //     .Include(b => b.BasketLines) // include the basket lines
            //     .FirstOrDefaultAsync(b => b.BasketId == basketId);

            return await context.Baskets.Include(sb => sb.BasketLines)
                .ThenInclude(line => line.Event)
                .Where(b => b.BasketId == basketId).FirstOrDefaultAsync();
        }

        public void AddBasket(Basket basket)
        {
            context.Baskets.Add(basket);
        }

        public async Task<bool> SaveChanges()
        {
            return (await context.SaveChangesAsync()) > 0;
        }

        public async Task ClearBasket(Guid basketId)
        {
            var basket = await GetBasketById(basketId);
            if (basket != null)
            {
                context.BasketLines.RemoveRange(basket.BasketLines);
                context.Baskets.Remove(basket);
            }
        }
    }
}