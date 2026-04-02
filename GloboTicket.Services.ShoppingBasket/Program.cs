using GloboTicket.Services.ShoppingBasket.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

using System;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GloboTicket.Services.ShoppingBasket
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();            

            var cacheMode = host.Services.GetRequiredService<IConfiguration>().GetValue<string>("ShoppingBasket:CacheMode");

            if (cacheMode == "SQLite")
            {

                using (var scope = host.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ShoppingBasketDbContext>();
                    db.Database.Migrate();
                }
            }

            host.Run();
        }                

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}