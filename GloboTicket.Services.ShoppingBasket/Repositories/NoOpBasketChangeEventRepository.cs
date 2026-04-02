using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.Entities;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class NoOpBasketChangeEventRepository : IBasketChangeEventRepository
    {
        public Task AddBasketEvent(BasketChangeEvent basketChangeEvent)
        {
            // Do nothing
            return Task.CompletedTask;
        }

        public Task<List<BasketChangeEvent>> GetBasketChangeEvents(DateTime startDate, int max)
        {
            // Return an empty list
            return Task.FromResult(new List<BasketChangeEvent>());
        }
    }
}