using Serilog;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.IDP;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(
        this WebApplicationBuilder builder)
    {
        // uncomment if you want to add a UI
        builder.Services.AddRazorPages();

        builder.Services.AddIdentityServer(options =>
            {
                // https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/api_scopes#authorization-based-on-scopes
                options.EmitStaticAudienceClaim = true;
                
                // Override the issuer URI to match host or container networking
                var publicOrigin = builder.Configuration["IDP_PUBLIC_ORIGIN"];
                if (!string.IsNullOrWhiteSpace(publicOrigin))
                {
                    options.IssuerUri = publicOrigin;
                }
            })
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryApiResources(Config.ApiResources)
            //.AddInMemoryClients(Config.Clients)
            .AddInMemoryClients(Config.GetClients(builder.Configuration))
            .AddTestUsers(TestUsers.Users)
            .AddExtensionGrantValidator<TokenExchangeExtensionGrantValidator>();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy());

        return builder.Build();
    }
    
    public static WebApplication ConfigurePipeline(this WebApplication app)
    { 
        app.UseSerilogRequestLogging();
    
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // uncomment if you want to add a UI
        app.UseStaticFiles();
        app.UseRouting();

        app.UseIdentityServer();

        // uncomment if you want to add a UI
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });

        app.MapRazorPages().RequireAuthorization();

        return app;
    }
}
