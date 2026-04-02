using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Globoticket.Services.IntegrationEventPublisher.DbContexts;
using Globoticket.Services.IntegrationEventPublisher.Repositories;
using Globoticket.Services.IntegrationEventPublisher.Worker;
using GloboTicket.Integration.MessagingBus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Globoticket.Services.IntegrationEventPublisher
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //services.AddDbContext<IntegrationEventsDbContext>(options =>
            //    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            // Use Cosmos instead of SQL Server
            services.AddDbContext<IntegrationEventsDbContext>(options =>
                options.UseCosmos(
                    Configuration["CosmosDb:Endpoint"],
                    Configuration["CosmosDb:Key"],
                    Configuration["CosmosDb:DatabaseName"]
                ));

            services.AddScoped<IIntegrationEventRepository, IntegrationEventRepository>();

            services.AddHostedService<EventPublisher>();

            //services.AddSingleton<IMessageBus, AzServiceBusMessageBus>();
            services.AddSingleton<IMessageBus>(sp =>
            {
                var connectionString = Configuration["ServiceBusConnectionString"] ?? throw new InvalidOperationException("ServiceBusConnectionString is not configured.");
                return new AzServiceBusMessageBus(connectionString);
            });

            //services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // app.UseHttpsRedirection();

            // app.UseRouting();

            // app.UseAuthorization();

            // app.UseEndpoints(endpoints =>
            // {
            //     endpoints.MapControllers();
            // });
        }
    }
}
