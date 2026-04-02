using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Payment.Services;
using GloboTicket.Services.Payment.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.Services.Payment
{
    public class Startup
    {
        private string _externalPaymentGatewayUri;
        private string _externalPaymentGatewaySource;
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var envExternalPaymentGatewayUri = Environment.GetEnvironmentVariable("APICONFIGS__EXTERNALPAYMENTGATEWAY__SERVICE_URI");            

            if (!string.IsNullOrWhiteSpace(envExternalPaymentGatewayUri))
            {
                _externalPaymentGatewayUri = envExternalPaymentGatewayUri;
                _externalPaymentGatewaySource = "environment variable (APICONFIGS__EXTERNALPAYMENTGATEWAY__SERVICE_URI)";
            }
            else
            {
                _externalPaymentGatewayUri = Configuration["ApiConfigs:ExternalPaymentGateway:Uri"];
                _externalPaymentGatewaySource = "configuration (ApiConfigs:ExternalPaymentGateway:Uri)";
            }

            if (string.IsNullOrWhiteSpace(_externalPaymentGatewayUri))
                throw new InvalidOperationException("ExternalPaymentGateway URI is not configured.");

            services.AddHostedService<ServiceBusListener>();
            services.AddHttpClient<IExternalGatewayPaymentService, ExternalGatewayPaymentService>(c =>
                c.BaseAddress = new Uri(_externalPaymentGatewayUri));

            //services.AddSingleton<IMessageBus, AzServiceBusMessageBus>();
            services.AddSingleton<IMessageBus>(sp =>
            {
                var connectionString = Configuration["ServiceBusConnectionString"] ?? throw new InvalidOperationException("ServiceBusConnectionString is not configured.");
                return new AzServiceBusMessageBus(connectionString);
            });

            services.AddControllers();
            services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy()); //always healthy (for liveness probe)
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("Payment → ExternalPaymentGateway base URI resolved to: {Uri} (source: {Source})", _externalPaymentGatewayUri, _externalPaymentGatewaySource);

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
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true });
            });
        }
    }
}