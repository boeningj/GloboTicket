using GloboTicket.Services.Marketing.DbContexts;
using GloboTicket.Services.Marketing.Repositories;
using GloboTicket.Services.Marketing.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using AutoMapper;
using GloboTicket.Services.Marketing.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.Services.Marketing
{
    public class Startup
    {
        private string _shoppingBasketUri;
        private string _shoppingBasketSource;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var envShoppingBasketUri = Environment.GetEnvironmentVariable("APICONFIGS__SHOPPINGBASKET__SERVICE_URI");            

            if (!string.IsNullOrWhiteSpace(envShoppingBasketUri))
            {
                _shoppingBasketUri = envShoppingBasketUri;
                _shoppingBasketSource = "environment variable (APICONFIGS__SHOPPINGBASKET__SERVICE_URI)";
            }
            else
            {
                _shoppingBasketUri = Configuration["ApiConfigs:ShoppingBasket:Uri"];
                _shoppingBasketSource = "configuration (ApiConfigs:ShoppingBasket:Uri)";
            }

            if (string.IsNullOrWhiteSpace(_shoppingBasketUri))
                throw new InvalidOperationException("ShoppingBasket URI is not configured.");

            services.AddControllers();

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            services.AddDbContext<MarketingDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
            });            
            // services.AddDbContext<MarketingDbContext>(options =>
            //     options.UseSqlite(Configuration.GetConnectionString("DefaultConnection_old")));

            //singleton DbContext for timed worker
            var optionsBuilder = new DbContextOptionsBuilder<MarketingDbContext>();            
            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            optionsBuilder.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
            //optionsBuilder.UseSqlite(Configuration.GetConnectionString("DefaultConnection_old"));

            //The BasketChangeEventRepository must be added as either a Singleton or as Scoped
            services.AddSingleton(new BasketChangeEventRepository(optionsBuilder.Options));

            // Register repository as scoped (because DbContext is scoped)
            //services.AddScoped<IBasketChangeEventRepository, BasketChangeEventRepository>();

            // Register the timed worker service
            services.AddHostedService<TimedBasketChangeEventService>();
            
            // Register HTTP client for calling ShoppingBasket API
            services.AddHttpClient<IBasketChangeEventService, BasketChangeEventService>(c =>
                c.BaseAddress = new Uri(_shoppingBasketUri));
            
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy()) //always healthy (for liveness probe)
                .AddSqlServer(Configuration.GetConnectionString("DefaultConnection"), timeout: TimeSpan.FromSeconds(3)); //checks DB connectivity (for readiness probe)
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Marketing → ShoppingBasket base URI resolved to: {Uri} (source: {Source})", _shoppingBasketUri, _shoppingBasketSource);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });
            });
        }
    }
}