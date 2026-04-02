using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using GloboTicket.Services.ShoppingBasket.Repositories;
using GloboTicket.Services.ShoppingBasket.Data;

namespace GloboTicket.Services.ShoppingBasket.Worker
{
    /// <summary>
    /// Background service that seeds events into the Redis (or in-memory) cache at application startup.
    /// </summary>
    public class RedisEventSeeder : IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<RedisEventSeeder> logger;

        public RedisEventSeeder(IServiceProvider serviceProvider, ILogger<RedisEventSeeder> logger)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Seeding Redis event cache...");

            using var scope = serviceProvider.CreateScope();
            var redisRepo = scope.ServiceProvider.GetRequiredService<IAzRedisEventRepository>();

            var events = EventSeedData.GetEvents();

            foreach (var ev in events)
            {
                // Only add if it doesn't already exist
                if (!await redisRepo.EventExists(ev.EventId))
                {
                    redisRepo.AddEvent(ev); // immediate write to cache
                    logger.LogInformation("Seeded event: {EventName}", ev.Name);
                }
            }

            logger.LogInformation("Redis event cache seeding completed.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No cleanup needed
            return Task.CompletedTask;
        }
    }
}