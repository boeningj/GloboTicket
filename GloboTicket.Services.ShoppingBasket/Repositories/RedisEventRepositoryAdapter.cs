using System;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.Entities;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    /// <summary>
    /// Adapter that implements IEventRepository by delegating to an underlying IAzRedisEventRepository.
    /// Works for both InMemory and Redis modes.
    /// </summary>
    public class RedisEventRepositoryAdapter : IEventRepository
    {
        private readonly IAzRedisEventRepository redisRepo;

        /// <summary>
        /// Creates a new adapter instance wrapping the specified repository.
        /// </summary>
        /// <param name="redisRepo">The underlying repository (in-memory or Redis).</param>
        public RedisEventRepositoryAdapter(IAzRedisEventRepository redisRepo)
        {
            this.redisRepo = redisRepo ?? throw new ArgumentNullException(nameof(redisRepo));
        }

        /// <summary>
        /// Adds an event to the underlying repository.
        /// </summary>
        /// <param name="theEvent">The event to add.</param>
        public void AddEvent(Event theEvent)
        {
            if (theEvent == null)
                throw new ArgumentNullException(nameof(theEvent));

            redisRepo.AddEvent(theEvent);
        }

        /// <summary>
        /// Checks whether an event exists in the underlying repository.
        /// </summary>
        /// <param name="eventId">The event ID to check.</param>
        /// <returns>True if exists; otherwise false.</returns>
        public Task<bool> EventExists(Guid eventId)
        {
            return redisRepo.EventExists(eventId);
        }

        /// <summary>
        /// Persists any changes to the underlying repository (no-op for Redis/in-memory).
        /// </summary>
        /// <returns>True if successful.</returns>
        public Task<bool> SaveChanges()
        {
            return redisRepo.SaveChanges();
        }
    }
}