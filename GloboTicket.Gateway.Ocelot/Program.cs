using System.IdentityModel.Tokens.Jwt; //For JwtSecurityTokenHandler - clear default inbound claim map
using GloboTicket.Gateway.DelegatingHandlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAccessTokenManagement(); // For IdentityModel.AspNetCore token cache

// Clear default claim mapping
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add authentication
var jwtOptions = builder.Configuration.GetSection("JwtOptions");

var authority = jwtOptions["Authority"];
var audience = jwtOptions["Audience"];
var requireHttps = bool.Parse(jwtOptions["RequireHttpsMetadata"] ?? "true");
var authenticationScheme = "GloboTicketGatewayAuthenticationScheme";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(authenticationScheme, options =>
    {
        options.Authority = authority;
        options.Audience = audience; //"globoticketgateway";
        // The below is needed so that Ocelot can locate the 'sub' claim to pass along in the request header as CurrentUser
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = requireHttps;
    });

// Needed by TokenExchangeDelegatingHandler to call the token exchange endpoint
builder.Services.AddHttpClient();
// Needed by TokenExchagneDelegatingHander to dynamically set the scope for the token exchange based on downstream route
//builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<EventCatalogTokenExchangeHandler>();
builder.Services.AddSingleton<ShoppingBasketTokenExchangeHandler>();
//builder.Services.AddScoped<TokenExchangeDelegatingHandler>();

// Add Ocelot configuration file
builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

// Add Ocelot with configuration & delegating handler for token exchange
// NOTE:  false tell Ocelot to resolve the handler from the DI container, so if it’s a singleton, it actually reuses it.
builder.Services.AddOcelot(builder.Configuration)
    .AddDelegatingHandler<EventCatalogTokenExchangeHandler>(false)
    .AddDelegatingHandler<ShoppingBasketTokenExchangeHandler>(false);
//.AddDelegatingHandler<TokenExchangeDelegatingHandler>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("JWT Authority={Authority}, Audience={Audience}, RequireHttps={RequireHttps}",
    authority, audience, requireHttps);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Authentication & authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Health checks for K8s (bypass Ocelot completely)
app.MapWhen(context => context.Request.Path.StartsWithSegments("/health"), healthApp =>
{
    healthApp.UseRouting();
    healthApp.UseEndpoints(endpoints =>
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });
    });
});

// Run Ocelot
await app.UseOcelot();

// NOTE - There's no UseRouting() / UseEndpoints() because Ocelot will not pass thru the
// request to any middleware that's put in the pipeline after it.  Ocelot fully handles 
// the request and returns the response.

app.Run();