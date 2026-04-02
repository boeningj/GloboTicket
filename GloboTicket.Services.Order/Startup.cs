using AutoMapper;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Ordering.DbContexts;
using GloboTicket.Services.Ordering.Extensions;
using GloboTicket.Services.Ordering.Helpers;
using GloboTicket.Services.Ordering.Messaging;
using GloboTicket.Services.Ordering.Repositories;
using GloboTicket.Services.Ordering.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.Services.Ordering
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
            // Needed by our TokenValidationService to validate incoming access tokens contained in messages from the Azure Service Bus
            services.AddHttpClient();
            services.AddScoped<TokenValidationService>();

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            services.AddDbContext<OrderDbContext>(options =>
            {
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
            });
            // services.AddDbContext<OrderDbContext>(options =>
            //     options.UseSqlite(Configuration.GetConnectionString("DefaultConnection_old")));

            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();

            //Specific DbContext for use from singleton AzServiceBusConsumer
            //var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
            // LocalDB will not work on Linux or MacOS so I'm going to use SQLite instead
            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            //optionsBuilder.UseSqlServer(Configuration.GetConnectionString("DefaultConnection_old"));
            //optionsBuilder.UseSqlite(Configuration.GetConnectionString("DefaultConnection"));

            //services.AddSingleton(new OrderRepository(optionsBuilder.Options));
            //services.AddSingleton(new CustomerRepository(optionsBuilder.Options));

            //services.AddSingleton<IMessageBus, AzServiceBusMessageBus>();
            services.AddSingleton<IMessageBus>(sp =>
            {
                var connectionString = Configuration["ServiceBusConnectionString"] ?? throw new InvalidOperationException("ServiceBusConnectionString is not configured.");
                return new AzServiceBusMessageBus(connectionString);
            });
            services.AddSingleton<IAzServiceBusConsumer, AzServiceBusConsumer>();
            
            services.AddHostedService<ServiceBusListener>();

            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy()) //always healthy (for liveness probe)
                .AddSqlServer(Configuration.GetConnectionString("DefaultConnection"), timeout: TimeSpan.FromSeconds(3)); //checks DB connectivity (for readiness probe)

            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Ordering API", Version = "v1" });
            });                        
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ordering API V1");

            });

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });
            });

            app.UseAzServiceBusConsumer();
        }
    }
}
