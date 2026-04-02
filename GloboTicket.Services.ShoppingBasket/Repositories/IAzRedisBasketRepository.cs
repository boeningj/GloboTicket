using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.Entities;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public interface IAzRedisBasketRepository
    {
        /// <summary>
        /// Gets a basket by id. Returns null if not found.
        /// </summary>
        Task<Basket> GetBasketById(Guid basketId);
        Task<bool> BasketExists(Guid basketId);
        Task ClearBasket(Guid basketId);
        Task<bool> AddBasket(Basket basket);
        Task<bool> SaveChanges(Basket basket);

        Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId);
        /// <summary>
        /// Gets a basket line by id. Returns null if not found.
        /// </summary>
        Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId);
        Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine);
        Task RemoveBasketLine(BasketLine basketLine);
    }
}