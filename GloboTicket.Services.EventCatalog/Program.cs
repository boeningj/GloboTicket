using GloboTicket.Services.EventCatalog.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using System.Diagnostics;
using System;

namespace GloboTicket.Services.EventCatalog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Enable detailed JWT/IdentityModel logging BEFORE building the host
            //IdentityModelEventSource.ShowPII = true;

            // Set up a TraceListener to pipe IdentityModel/System.Diagnostics.Trace to console so you can see IdentityModel logging
            /*
            Trace.Listeners.Clear(); // optional: remove default listeners
            Trace.Listeners.Add(new ConsoleTraceListener());

            // Optional: make Trace verbose
            Trace.AutoFlush = true;
            Trace.WriteLine("=== IdentityModel tracing enabled ===");
            */

            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())                    
            {
                var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                {
                    // var db = scope.ServiceProvider.GetRequiredService<EventCatalogDbContext>();
                    // db.Database.Migrate();
                    
                    //Uncomment only for development
                    //var dbCosmos = scope.ServiceProvider.GetRequiredService<EventCatalogCosmosDbContext>();
                    //dbCosmos.Database.EnsureDeleted();
                    //dbCosmos.Database.EnsureCreated();
                }
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                // .ConfigureLogging(logging =>
                // {
                //     logging.ClearProviders();
                //     logging.AddConsole();
                //     logging.SetMinimumLevel(LogLevel.Trace);
                // })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
