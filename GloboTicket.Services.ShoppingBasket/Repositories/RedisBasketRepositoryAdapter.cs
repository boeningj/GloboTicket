using GloboTicket.Services.ShoppingBasket.Entities;
using GloboTicket.Services.ShoppingBasket.Repositories;
using GloboTicket.Services.ShoppingBasket.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class RedisBasketRepositoryAdapter : IBasketRepository, IBasketLinesRepository
    {
        private readonly IAzRedisBasketRepository redisRepo;

        // Track the last basket that was changed so SaveChanges(Basket) can be called.
        // We keep both for convenience.
        private Basket lastModifiedBasket;
        private Guid? lastModifiedBasketId;

        public RedisBasketRepositoryAdapter(IAzRedisBasketRepository redisRepo)
        {
            this.redisRepo = redisRepo ?? throw new ArgumentNullException(nameof(redisRepo));
        }

        // -------- IBasketRepository --------
        public async Task<bool> BasketExists(Guid basketId)
        {
            return await redisRepo.BasketExists(basketId);
        }

        public async Task<Basket> GetBasketById(Guid basketId)
        {
            return await redisRepo.GetBasketById(basketId);
        }

        public void AddBasket(Basket basket)
        {
            // AzRedis repo AddBasket might be async in your code; adapt if needed.
            // If your redisRepo.AddBasket returns Task<bool>, call .GetAwaiter().GetResult()
            // or make this method async. Here we assume it returns Task<bool>.
            var t = redisRepo.AddBasket(basket);
            // track the basket so SaveChanges can commit it
            lastModifiedBasket = basket;
            lastModifiedBasketId = basket?.BasketId;
        }

        public async Task<bool> SaveChanges()
        {
            if (lastModifiedBasket == null && lastModifiedBasketId.HasValue)
            {
                // try to get the up-to-date basket from redis repo
                lastModifiedBasket = await redisRepo.GetBasketById(lastModifiedBasketId.Value);
            }

            if (lastModifiedBasket == null)
                throw new InvalidOperationException("No basket available to save to Redis.");

            var result = await redisRepo.SaveChanges(lastModifiedBasket);

            // clear tracking on success
            if (result)
            {
                lastModifiedBasket = null;
                lastModifiedBasketId = null;
            }

            return result;
        }

        public async Task ClearBasket(Guid basketId)
        {
            await redisRepo.ClearBasket(basketId);
            // if we were tracking this basket, clear it
            if (lastModifiedBasketId.HasValue && lastModifiedBasketId.Value == basketId)
            {
                lastModifiedBasket = null;
                lastModifiedBasketId = null;
            }
        }

        // -------- IBasketLinesRepository --------

        public async Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId)
        {
            return await redisRepo.GetBasketLines(basketId);
        }

        public async Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId)
        {
            return await redisRepo.GetBasketLineById(basketId, basketLineId);
        }

        public async Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine)
        {
            var result = await redisRepo.AddOrUpdateBasketLine(basketId, basketLine);

            // update tracking so SaveChanges() can later persist the basket
            lastModifiedBasketId = basketId;
            // fetch the updated basket (safe) so we can pass to SaveChanges(Basket)
            lastModifiedBasket = await redisRepo.GetBasketById(basketId);

            return result;
        }

        public async Task UpdateBasketLine(BasketLine basketLine)
        {
            if (basketLine == null)
                throw new ArgumentNullException(nameof(basketLine));

            // Use AddOrUpdate since the Redis repo has no dedicated Update method
            await redisRepo.AddOrUpdateBasketLine(basketLine.BasketId, basketLine);

            // Track the modified basket so SaveChanges() knows which one to persist
            lastModifiedBasketId = basketLine.BasketId;

            // Refresh the local copy for SaveChanges
            lastModifiedBasket = await redisRepo.GetBasketById(basketLine.BasketId);
        }

        public async Task RemoveBasketLine(BasketLine basketLine)
        {
            if (basketLine == null)
                throw new ArgumentNullException(nameof(basketLine));

            // Ask Redis repo to remove the basket line
            await redisRepo.RemoveBasketLine(basketLine);

            // Track which basket was modified
            lastModifiedBasketId = basketLine.BasketId;

            // Refresh local basket so SaveChanges() can persist the new state
            lastModifiedBasket = await redisRepo.GetBasketById(basketLine.BasketId);
        }

        // NOTE: IBasketRepository and IBasketLinesRepository both require SaveChanges().
        // This single implementation satisfies both interface requirements.
    }
}