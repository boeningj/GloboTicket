using Globoticket.Services.IntegrationEventPublisher.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Globoticket.Services.IntegrationEventPublisher.DbContexts
{
    public class IntegrationEventsDbContext : DbContext
    {
        public IntegrationEventsDbContext(DbContextOptions<IntegrationEventsDbContext> options) : base(options)
        {
// #if DEBUG
//     var entityType = this.Model.FindEntityType(typeof(IntegrationEventLogEntry));
//     if (entityType != null)
//     {
//         Console.WriteLine("=== EF Model Info for IntegrationEventLogEntry ===");
//         foreach (var prop in entityType.GetProperties())
//         {
//             Console.WriteLine($"Property: {prop.Name}, Type: {prop.ClrType.Name}");
//         }

//         var container = entityType.GetContainer();
//         Console.WriteLine($"Container: {container ?? "(none)"}");

//         var partitionKeyProperty = entityType.GetProperties()
//             .FirstOrDefault(p => p.IsPrimaryKey() == false && p.Name.Contains("PartitionKey", StringComparison.OrdinalIgnoreCase));
//         Console.WriteLine($"Partition key property detected: {partitionKeyProperty?.Name ?? "(none)"}");

//         Console.WriteLine("===============================================");
//     }
// #endif
        }
        public DbSet<IntegrationEventLogEntry> IntegrationEventLogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<IntegrationEventLogEntry>(entity => {
                entity.ToContainer("IntegrationEventLogs");
                entity.HasPartitionKey(e => e.PartitionKey);
                entity.HasNoDiscriminator();
                entity.Property(e => e.IntegrationEventLogId).ToJsonProperty("id");
            });
        }
    }
}
