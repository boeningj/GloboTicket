using GloboTicket.Services.ShoppingBasket.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public interface IBasketLinesRepository
    {
        Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId);

        Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId);

        Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine);

        Task UpdateBasketLine(BasketLine basketLine);

        Task RemoveBasketLine(BasketLine basketLine);

        Task<bool> SaveChanges();        
    }
}