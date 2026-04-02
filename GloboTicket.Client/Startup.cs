using GloboTicket.Web.Models;
using GloboTicket.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Collections.Generic;
using System.Net.Http;

namespace GloboTicket.Web
{
    public class Startup
    {
        private readonly IHostEnvironment environment;
        private readonly IConfiguration config;

        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            config = configuration;
            this.environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            // For TrustGateway mode
            services.AddTransient<UserTokenForwardingHandler>();

            var authMode = config.GetValue<string>("AuthenticationOptions:AuthMode") ?? throw new InvalidOperationException("AuthenticationOptions:AuthMode is not configured.");
            var disableCertValidation = config.GetValue<bool>("OIDC:DISABLECERTVALIDATION", false);

            IMvcBuilder mvcBuilder;
            if (authMode != "None")
            { 
                var requireAuthenticatedUserPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                mvcBuilder = services.AddControllersWithViews(options =>
                {
                    options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUserPolicy));
                });
            }
            else
            {
                mvcBuilder = services.AddControllersWithViews();
            }

            if (environment.IsDevelopment())
                mvcBuilder.AddRazorRuntimeCompilation();
            
            var eventCatalogBaseUri = String.Empty;
            var shoppingBasketBaseUri = String.Empty;
            var orderBaseUri = String.Empty;
            var oidcClientId = String.Empty;
            var oidcClientSecret = String.Empty;

            // Prefer explicit OIDC__CLIENT* (K8s / App Service)
            var directClientId = config["OIDC:ClientId"];
            var directClientSecret = config["OIDC:ClientSecret"];

            if (!string.IsNullOrEmpty(directClientId))
            {
                oidcClientId = directClientId;
                oidcClientSecret = directClientSecret;
            }
            else
            {
                // Fallback for local multi-mode flexibility
                if (authMode == "TrustGateway")
                {
                    oidcClientId = config["OIDC:Gateway:ClientId"];
                    oidcClientSecret = config["OIDC:Gateway:ClientSecret"];
                }
                else
                {
                    oidcClientId = config["OIDC:Interactive:ClientId"];
                    oidcClientSecret = config["OIDC:Interactive:ClientSecret"];
                }
            }

            if (string.IsNullOrWhiteSpace(oidcClientId))
                throw new InvalidOperationException("OIDC client id is not configured.");

            if (string.IsNullOrWhiteSpace(oidcClientSecret))
                throw new InvalidOperationException("OIDC client secret is not configured.");

            // Start empty and add based on mode
            var oidcScopes = new List<string>
            {
                "openid",
                "profile"
            };

            switch (authMode) 
            {
                case "AccessTokens":
                    // Middleware that's used to get the Access Token to pass to our API as the Bearer Token on each request
                    services.AddAccessTokenManagement();
                    eventCatalogBaseUri = config["ApiConfigs:EventCatalog:Uri"];
                    shoppingBasketBaseUri = config["ApiConfigs:ShoppingBasket:Uri"];
                    orderBaseUri = config["ApiConfigs:Order:Uri"];                    
                    oidcScopes.Add("shoppingbasket.fullaccess");
                    oidcScopes.Add("eventcatalog.read");
                    oidcScopes.Add("eventcatalog.write");
                    oidcScopes.Add("offline_access"); // Required for refresh tokens / long-lived access
                break;
                case "TrustGateway":
                    eventCatalogBaseUri = config["ApiConfigs:EventCatalog:GatewayUri"];
                    shoppingBasketBaseUri = config["ApiConfigs:ShoppingBasket:GatewayUri"];
                    orderBaseUri = config["ApiConfigs:Order:GatewayUri"];                    
                    oidcScopes.Add("globoticketgateway.fullaccess");                    
                break;
                case "None":
                default:
                    eventCatalogBaseUri = config["ApiConfigs:EventCatalog:Uri"];
                    shoppingBasketBaseUri = config["ApiConfigs:ShoppingBasket:Uri"];
                    orderBaseUri = config["ApiConfigs:Order:Uri"];
                break;
            }

            if (string.IsNullOrWhiteSpace(eventCatalogBaseUri))
                throw new InvalidOperationException("EventCatalog base URI is not configured.");
            if (string.IsNullOrWhiteSpace(shoppingBasketBaseUri))
                throw new InvalidOperationException("ShoppingBasket base URI is not configured.");
            if (string.IsNullOrWhiteSpace(orderBaseUri))
                throw new InvalidOperationException("Order base URI is not configured.");

            var eventCatalogClient = services.AddHttpClient<IEventCatalogService, EventCatalogService>(c => c.BaseAddress = new Uri(eventCatalogBaseUri))
                .ConfigurePrimaryHttpMessageHandler(() => CreateHttpHandler(disableCertValidation));
            if (authMode == "AccessTokens")
                eventCatalogClient.AddUserAccessTokenHandler();
            else if (authMode == "TrustGateway")
                eventCatalogClient.AddHttpMessageHandler<UserTokenForwardingHandler>();

            var shoppingBasketClient = services.AddHttpClient<IShoppingBasketService, ShoppingBasketService>(c => c.BaseAddress = new Uri(shoppingBasketBaseUri))
                .ConfigurePrimaryHttpMessageHandler(() => CreateHttpHandler(disableCertValidation));
            if (authMode == "AccessTokens")
                shoppingBasketClient.AddUserAccessTokenHandler();
            else if (authMode == "TrustGateway")
                shoppingBasketClient.AddHttpMessageHandler<UserTokenForwardingHandler>();
            
            services.AddHttpClient<IOrderService, OrderService>(c => c.BaseAddress = new Uri(orderBaseUri))
                .ConfigurePrimaryHttpMessageHandler(() => CreateHttpHandler(disableCertValidation));
            //TODO - I belive this can be commented out since the client no longer directly calls into the discount service to 'apply' a coupon code anymore
            services.AddHttpClient<IDiscountService, DiscountService>(c => c.BaseAddress = new Uri(config["ApiConfigs:Discount:Uri"]))
                .ConfigurePrimaryHttpMessageHandler(() => CreateHttpHandler(disableCertValidation));

            services.AddSingleton<Settings>();                        

            if (authMode != "None")
            {                
                // NOTE:  Prior to .NET 8.0, this was defined in JWT security token handler
                JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = config["Identity:Authority"];
                    options.ClientId = oidcClientId;
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    options.ClientSecret = oidcClientSecret;                    
                    options.GetClaimsFromUserInfoEndpoint = true;
                    // Containerized dev: uses HTTP, disables HTTPS metadata validation (false).
                    // Local dev or production: uses HTTPS, keeps validation enabled (true).
                    options.RequireHttpsMetadata = bool.Parse(config["CLIENT__REQUIREHTTPSMETADATA"] ?? "true");

                    if (disableCertValidation)
                    {
                        options.BackchannelHttpHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        };
                    }

                    foreach (var scope in oidcScopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
            }
        }

        private HttpMessageHandler CreateHttpHandler(bool disableCertValidation)
        {
            if (disableCertValidation)
            {
                return new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            }

            return new HttpClientHandler();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            var authMode = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<string>("AuthenticationOptions:AuthMode");
            logger.LogInformation("GloboTicket.Client AuthMode: {AuthMode}", authMode);

            if (authMode == "AccessTokens" || authMode == "TrustGateway")
            {
                app.UseAuthentication(); //must go before UseAuthorization
                app.UseAuthorization();
            }
            else
            {
                logger.LogWarning("Authentication is disabled! All endpoints are anonymous.");
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=EventCatalog}/{action=Index}/{id?}");
            });
        }
    }
}
