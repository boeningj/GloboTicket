using GloboTicket.Services.Marketing.DbContexts;
using GloboTicket.Services.Marketing.Entities;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GloboTicket.Services.Marketing.Repositories
{
    public class BasketChangeEventRepository : IBasketChangeEventRepository
    {
        private readonly DbContextOptions<MarketingDbContext> dbContextOptions;
        //private readonly MarketingDbContext _db;

        public BasketChangeEventRepository(DbContextOptions<MarketingDbContext> dbContextOptions)
        {
            this.dbContextOptions = dbContextOptions;
        }

        // public BasketChangeEventRepository(MarketingDbContext db)
        // {
        //     _db = db;
        // }

        public async Task AddBasketChangeEvent(BasketChangeEvent basketChangeEvent)
        {
            await using (var marketingDbContext = new MarketingDbContext(dbContextOptions))
            {
                await marketingDbContext.BasketChangeEvents.AddAsync(basketChangeEvent);
                await marketingDbContext.SaveChangesAsync();
            }
        }

        // public async Task AddBasketChangeEvent(BasketChangeEvent basketChangeEvent)
        // {
        //     await _db.BasketChangeEvents.AddAsync(basketChangeEvent);
        //     await _db.SaveChangesAsync();
        // }
    }
}
