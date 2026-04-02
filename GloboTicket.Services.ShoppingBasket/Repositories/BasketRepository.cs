using GloboTicket.Services.ShoppingBasket.DbContexts;
using GloboTicket.Services.ShoppingBasket.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;  // for Collection<T>

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class BasketRepository : IBasketRepository
    {
        private readonly ShoppingBasketDbContext shoppingBasketDbContext;

        public BasketRepository(ShoppingBasketDbContext shoppingBasketDbContext)
        {
            this.shoppingBasketDbContext = shoppingBasketDbContext;
        }

        public async Task<Basket> GetBasketById(Guid basketId)
        {
            return await shoppingBasketDbContext.Baskets.Include(sb => sb.BasketLines)
                .ThenInclude(line => line.Event)
                .Where(b => b.BasketId == basketId).FirstOrDefaultAsync();
        }

        public async Task<bool> BasketExists(Guid basketId)
        {
            return await shoppingBasketDbContext.Baskets
                .AnyAsync(b => b.BasketId == basketId);
        }

        public async Task ClearBasket(Guid basketId)
        {
            var basketLinesToClear = shoppingBasketDbContext.BasketLines.Where(b => b.BasketId == basketId);
            shoppingBasketDbContext.BasketLines.RemoveRange(basketLinesToClear);

            var basket = shoppingBasketDbContext.Baskets.FirstOrDefault(b => b.BasketId == basketId);
            if (basket != null) basket.CouponId = null;

            await SaveChanges();
        }

        public void AddBasket(Basket basket)
        {
            shoppingBasketDbContext.Baskets.Add(basket);
        }

        public async Task<bool> SaveChanges()
        {
            return (await shoppingBasketDbContext.SaveChangesAsync() > 0);
        }



        #region Basket Line Methods

        public async Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId)
        {
            var basket = await GetBasketById(basketId);
            return basket?.BasketLines ?? Enumerable.Empty<BasketLine>();
        }

        public async Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId)
        {
            var basket = await GetBasketById(basketId);
            return basket?.BasketLines.FirstOrDefault(bl => bl.BasketLineId == basketLineId);
        }

        public async Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine)
        {
            var basket = await GetBasketById(basketId);
            if (basket.BasketLines == null)
                basket.BasketLines = new Collection<BasketLine>();

            var existingLine = basket.BasketLines
                .FirstOrDefault(bl => bl.BasketLineId == basketLine.BasketLineId);

            if (existingLine == null)
            {
                basketLine.BasketId = basketId;
                basketLine.BasketLineId = Guid.NewGuid();
                basket.BasketLines.Add(basketLine);
            }
            else
            {
                existingLine.Event = basketLine.Event;
                existingLine.EventId = basketLine.EventId;
                existingLine.Price = basketLine.Price;
                existingLine.TicketAmount = basketLine.TicketAmount;
            }

            await SaveChanges();
            return basketLine;
        }

        public void UpdateBasketLine(BasketLine basketLine)
        {
            shoppingBasketDbContext.BasketLines.Update(basketLine);
        }

        public Task RemoveBasketLine(BasketLine basketLine)
        {
            shoppingBasketDbContext.BasketLines.Remove(basketLine);
            return Task.CompletedTask;
        }

        #endregion
    }
}
