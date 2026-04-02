using GloboTicket.Services.ShoppingBasket.Entities;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

using System.Text;
using System.Text.Json;

using System.Collections.Generic;
using System.Collections.ObjectModel;  // for Collection<T>
using System.Linq;                     // for FirstOrDefault()
using Microsoft.Extensions.Logging;
using GloboTicket.Services.ShoppingBasket.Services;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class AzRedisBasketRepository : IAzRedisBasketRepository
    {
        private readonly IDistributedCache distributedCache;
        public IConfiguration Configuration { get; }
        private readonly ILogger<AzRedisBasketRepository> logger;
        //private Basket currentBasket; // holds basket until SaveChanges
        private readonly IEventCatalogService eventCatalogService;

        public AzRedisBasketRepository(IDistributedCache distributedCache, IConfiguration configuration, ILogger<AzRedisBasketRepository> logger, IEventCatalogService eventCatalogService)
        {
            this.distributedCache = distributedCache;
            Configuration = configuration;
            this.logger = logger;
            this.eventCatalogService = eventCatalogService;
        }

        #region Serialization Helpers for Redis Cache      

        private byte[] SerializeBasket(Basket basket)
        {
            var json = JsonSerializer.Serialize(basket);
            return Encoding.UTF8.GetBytes(json);
        }

        private Basket DeserializeBasket(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            var basket = JsonSerializer.Deserialize<Basket>(json);

            if (basket == null)
            {
                logger.LogWarning("Failed to deserialize basket from Redis. Returning empty basket. Raw JSON: {Json}", json);
                return new Basket();
            }

            return basket;
        }
        #endregion

        #region Basket Functions

        public async Task<Basket> GetBasketById(Guid basketId)
        {
            var basket = await distributedCache.GetAsync(basketId.ToString());
            return basket is null ? null : DeserializeBasket(basket);
            // return basket is null ? new Basket { BasketId = basketId, BasketLines = new Collection<BasketLine>() }
            //               : DeserializeBasket(basket);
        }

        public async Task<bool> BasketExists(Guid basketId)
        {
            var basket = await distributedCache.GetAsync(basketId.ToString());
            return basket != null;
        }

        public async Task ClearBasket(Guid basketId)
        {
            await distributedCache.RemoveAsync(basketId.ToString());
        }

        public async Task<bool> AddBasket(Basket basket)
        {
            // assign a new BasketId if it doesn’t have one yet
            if (basket.BasketId == Guid.Empty)
                basket.BasketId = Guid.NewGuid();

            return await SaveChanges(basket);
        }

        public async Task<bool> SaveChanges(Basket basket)
        {
            if (basket == null)
                throw new ArgumentNullException(nameof(basket));

            byte[] serializedBasket = SerializeBasket(basket);

            int cacheDuration = int.Parse(Configuration["CacheDurationMinutes"]);

            var options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheDuration));

            await distributedCache.SetAsync(basket.BasketId.ToString(), serializedBasket, options);

            return true;
        }        

        #endregion

        #region Basket Lines

        public async Task<IEnumerable<BasketLine>> GetBasketLines(Guid basketId)
        {
            var basket = await GetBasketById(basketId);
            //return basket.BasketLines;
            return basket.BasketLines ?? Enumerable.Empty<BasketLine>();
        }

        public async Task<BasketLine> GetBasketLineById(Guid basketId, Guid basketLineId)
        {
            var basket = await GetBasketById(basketId);
            return basket.BasketLines.FirstOrDefault(bl => bl.BasketLineId == basketLineId)
                   ?? new BasketLine { BasketId = basketId };
        }

        public async Task<BasketLine> AddOrUpdateBasketLine(Guid basketId, BasketLine basketLine)
        {
            logger.LogInformation("Inside repo AddOrUpdateBasketLine: BasketId={BasketId}, BasketLineId={BasketLineId}",
                basketId, basketLine.BasketLineId);

            var basket = await GetBasketById(basketId);

            if (basket.BasketLines == null)
            {
                basket.BasketLines = new Collection<BasketLine>();
            }

            // Fetch Event from EventCatalogService
            if (basketLine.Event == null)
            {
                var eventFromCatalog = await eventCatalogService.GetEvent(basketLine.EventId);
                basketLine.Event = eventFromCatalog;
            }

            var existingLine = basket.BasketLines.FirstOrDefault(line => line.BasketLineId == basketLine.BasketLineId);

            if (existingLine == null)
            {
                // create new basket line

                basketLine.BasketId = basketId;
                basketLine.BasketLineId = Guid.NewGuid();

                basket.BasketLines.Add(basketLine);

                logger.LogInformation("Inside repo - new IDs assigned: BasketId={BasketId}, BasketLineId={BasketLineId}",
                    basketLine.BasketId, basketLine.BasketLineId);
            }
            else
            {
                // update existing basket line

                existingLine.Event = basketLine.Event;
                existingLine.EventId = basketLine.EventId;
                existingLine.Price = basketLine.Price;
                existingLine.TicketAmount = basketLine.TicketAmount;

                logger.LogInformation("Inside repo - updating existing line: BasketId={BasketId}, BasketLineId={BasketLineId}",
                    existingLine.BasketId, existingLine.BasketLineId);
            }

            await this.SaveChanges(basket);            

            //return basketLine;
            // RETURN the actual basket line from the basket, not the input object
            //return basket.BasketLines.First(bl => bl.BasketLineId == (existingLine?.BasketLineId ?? basketLine.BasketLineId));
            var lineToReturn = existingLine ?? basketLine;
            logger.LogInformation("Returning from repo: BasketId={BasketId}, BasketLineId={BasketLineId}",
                lineToReturn.BasketId, lineToReturn.BasketLineId);
            return lineToReturn;

        }

        public void UpdateBasketLine(BasketLine basketLine)
        {
            // Stateless repo: changes applied immediately in AddOrUpdateBasketLine
            if (basketLine == null)
                throw new ArgumentNullException(nameof(basketLine));
        }

        public async Task RemoveBasketLine(BasketLine basketLine)
        {
            var basket = await GetBasketById(basketLine.BasketId);

            if (basket.BasketLines == null)
                basket.BasketLines = new Collection<BasketLine>();

            var existingLine = basket.BasketLines.FirstOrDefault(bl => bl.BasketLineId == basketLine.BasketLineId);
            if (existingLine != null)
            {
                basket.BasketLines.Remove(existingLine);
                await SaveChanges(basket);
            }
        }

        #endregion
    }
}
    

