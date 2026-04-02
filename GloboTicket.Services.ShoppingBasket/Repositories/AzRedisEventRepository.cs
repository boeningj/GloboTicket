using System;
using System.Text.Json;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    /// <summary>
    /// Redis/in-memory cache implementation of IAzRedisEventRepository.
    /// Works with both InMemory (IDistributedCache) and Azure Redis.
    /// </summary>
    public class AzRedisEventRepository : IAzRedisEventRepository
    {
        private readonly IDistributedCache distributedCache;
        private readonly ILogger<AzRedisEventRepository> logger;

        public AzRedisEventRepository(IDistributedCache distributedCache, ILogger<AzRedisEventRepository> logger)
        {
            this.distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Serialization Helpers

        private byte[] SerializeEvent(Event theEvent)
        {
            var json = JsonSerializer.Serialize(theEvent);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        private Event DeserializeEvent(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            var json = System.Text.Encoding.UTF8.GetString(data);
            try
            {
                return JsonSerializer.Deserialize<Event>(json);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize Event from cache. Returning null. Raw JSON: {Json}", json);
                return null;
            }
        }

        #endregion

        #region IAzRedisEventRepository Methods

        public void AddEvent(Event theEvent)
        {
            if (theEvent == null)
                throw new ArgumentNullException(nameof(theEvent));

            byte[] serialized = SerializeEvent(theEvent);

            // Use default options; could customize expiration if needed
            var options = new DistributedCacheEntryOptions();

            distributedCache.Set($"event:{theEvent.EventId}", serialized, options);

            logger.LogInformation("Event added to cache: {EventId}", theEvent.EventId);
        }

        public async Task<bool> EventExists(Guid eventId)
        {
            var data = await distributedCache.GetAsync($"event:{eventId}");
            return data != null;
        }

        public Task<bool> SaveChanges()
        {
            // Stateless cache writes; changes are applied immediately
            return Task.FromResult(true);
        }

        #endregion
    }
}