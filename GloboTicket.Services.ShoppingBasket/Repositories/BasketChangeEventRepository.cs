using System;
using System.Collections.Generic;
using System.Linq;
using GloboTicket.Services.ShoppingBasket.DbContexts;
using GloboTicket.Services.ShoppingBasket.Entities;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace GloboTicket.Services.ShoppingBasket.Repositories
{
    public class BasketChangeEventRepository: IBasketChangeEventRepository
    {
        private readonly ShoppingBasketDbContext shoppingBasketDbContext;

        public BasketChangeEventRepository(ShoppingBasketDbContext shoppingBasketDbContext)
        {
            this.shoppingBasketDbContext = shoppingBasketDbContext;
        }

        public async Task AddBasketEvent(BasketChangeEvent basketChangeEvent)
        {
            await shoppingBasketDbContext.BasketChangeEvents.AddAsync(basketChangeEvent);
            await shoppingBasketDbContext.SaveChangesAsync();
        }

        public async Task<List<BasketChangeEvent>> GetBasketChangeEvents(DateTime startDate, int max)
        {
            /*
            This throws an exception:
            An unhandled exception has occurred while executing the request. 
            System.InvalidOperationException: The LINQ expression 'DbSet<BasketChangeEvent>() .Where(b => b.InsertedAt > __p_0)' could not be translated.
            Either rewrite the query in a form that can be translated, or switch to client evaluation explicitly by inserting a call to
            'AsEnumerable', 'AsAsyncEnumerable', 'ToList', or 'ToListAsync'. See https://go.microsoft.com/fwlink/?linkid=2101038 for more information.
            at Microsoft.EntityFrameworkCore.Query.QueryableMethodTranslatingExpressionVisitor.Translate(Expression expression)
            
            That error is very common when upgrading demos between EF Core versions.
            The issue is that EF Core can’t translate the LINQ expression:
                .Where(b => b.InsertedAt > __p_0)
            That means your BasketChangeEvent.InsertedAt is probably a DateTime (or DateTimeOffset),
            and EF Core SQLite provider in .NET 8 doesn’t know how to compare it directly in SQL.
            You need to move part of that query to client evaluation by pulling the data into memory,
            then filtering in LINQ-to-Objects. 

            When using a SQL Server database, the .Where(b => b.InsertedAt > startDate) will work fine,
            but it will not work with SQLite as SQLite doesn’t have a native DateTimeOffset type at all.
            When EF Core targets SQLite, DateTimeOffset ends up stored as TEXT (ISO8601 string) or sometimes
            as ticks (INTEGER), depending on the EF Core version and configuration.
            The problem:
            SQL Server knows how to compare datetimeoffset columns against a .NET DateTimeOffset.
            SQLite provider can’t reliably translate b.InsertedAt > startDate into valid SQL, because it doesn’t
            know how to compare two TEXT (or INTEGER) values in all cases.

            Hence why you’re getting:  The LINQ expression could not be translated.
            */

            /* This is for SQL Server
            return await shoppingBasketDbContext.BasketChangeEvents.Where(b => b.InsertedAt > startDate)
                .OrderBy(b => b.InsertedAt).Take(max).ToListAsync();
            */

            // var events = await shoppingBasketDbContext.BasketChangeEvents
            //     .OrderBy(b => b.InsertedAt)
            //     .Take(max)
            //     .ToListAsync();

            // return events.Where(b => b.InsertedAt > startDate).ToList();

            var events = await shoppingBasketDbContext.BasketChangeEvents.ToListAsync(); // fetch everything

            return events
                .Where(b => b.InsertedAt > startDate) // filter in memory
                .OrderBy(b => b.InsertedAt)
                .Take(max)
                .ToList();

            /*
            Another way to do it in SQLite is with:
            return await shoppingBasketDbContext.BasketChangeEvents
                .AsEnumerable() // forces EF to stop translating here
                .Where(b => b.InsertedAt > startDate)
                .OrderBy(b => b.InsertedAt)
                .Take(max)
                .ToList();

            The difference:
            The .AsEnumerable causes EF to load all rows from the table first, then LINQ-to-Objects applies filtering/sorting/take.
            That means you might fetch unnecessary data from SQLite.

            My split query above:
            EF translates only the OrderBy + Take into SQL (so SQLite does some work).
            Filtering on InsertedAt > startDate happens after the query, in memory.
            This is slightly more efficient than .AsEnumerable() if your table is big, because at least you’re
            not loading all rows.
            */

        }
    }
}
