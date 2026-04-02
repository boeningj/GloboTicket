using GloboTicket.Services.Ordering.Entities;
using Microsoft.EntityFrameworkCore;

namespace GloboTicket.Services.Ordering.DbContexts
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessedMessage>()
                .HasKey(pm => pm.MessageId);
        }
    }
}
