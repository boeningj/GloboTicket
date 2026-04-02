using AutoMapper;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.ShoppingBasket.DbContexts;
using GloboTicket.Services.ShoppingBasket.Repositories;
using GloboTicket.Services.ShoppingBasket.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;
using GloboTicket.Grpc;
using GloboTicket.Services.ShoppingBasket.Worker;
using Grpc.Core;

using CorrelationId;
using CorrelationId.DependencyInjection;   // for AddCorrelationId extension method
using CorrelationId.HttpClient;            // if you want HttpClient propagation
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using IdentityModel;
using IdentityModel.Client;
using GloboTicket.Services.ShoppingBasket.Helpers;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.Services.ShoppingBasket
{
    public class Startup
    {
        private string _eventCatalogUri;
        private string _eventCatalogSource;
        private string _discountUri;
        private string _discountSource;
        private string _identityAuthority;
        private string _jwtAuthority;
        private bool _requireHttps;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var envEventCatalogUri = Environment.GetEnvironmentVariable("APICONFIGS__EVENTCATALOG__SERVICE_URI");

            if (!string.IsNullOrWhiteSpace(envEventCatalogUri))
            {
                _eventCatalogUri = envEventCatalogUri;
                _eventCatalogSource = "environment variable (APICONFIGS__EVENTCATALOG__SERVICE_URI)";
            }
            else
            {
                _eventCatalogUri = Configuration["ApiConfigs:EventCatalog:Uri"];
                _eventCatalogSource = "configuration (ApiConfigs:EventCatalog:Uri)";
            }

            if (string.IsNullOrWhiteSpace(_eventCatalogUri))
                throw new InvalidOperationException("EventCatalog URI is not configured.");

            var envDiscountUri = Environment.GetEnvironmentVariable("APICONFIGS__DISCOUNT__SERVICE_URI");

            if (!string.IsNullOrWhiteSpace(envDiscountUri))
            {
                _discountUri = envDiscountUri;
                _discountSource = "environment variable (APICONFIGS__DISCOUNT__SERVICE_URI)";
            }
            else
            {
                _discountUri = Configuration["ApiConfigs:Discount:Uri"];
                _discountSource = "configuration (ApiConfigs:Discount:Uri)";
            }

            if (string.IsNullOrWhiteSpace(_discountUri))
                throw new InvalidOperationException("Discount URI is not configured.");

            services.AddDefaultCorrelationId(options =>
            {
                options.AddToLoggingScope = true;       // Include in ILogger scopes
                options.EnforceHeader = false;          // Don't require clients to send one
                options.IncludeInResponse = true;       // Returns ID in response headers
                options.RequestHeader = "X-Correlation-ID";
                options.ResponseHeader = "X-Correlation-ID";
            });

            services.AddControllers();

            //services.AddHttpContextAccessor();
            //services.AddScoped<TokenExchangeService>();

            var authMode = Configuration.GetValue<string>("AuthenticationOptions:AuthMode") ?? throw new InvalidOperationException("AuthenticationOptions:AuthMode is not configured.");
            switch (authMode) 
            {
                case "AccessTokens":
                {
                    // -------------------------------------------------------------------
                    // Configure M2M access to EventCatalog service (AccessTokens mode)
                    //
                    // - Uses Client Credentials flow via Identity Provider to request an access token
                    // - Named token client: "eventcatalog"
                    // - Token scope: eventcatalog.read
                    // - The token is automatically attached to outgoing HTTP requests
                    //   by AddClientAccessTokenHandler when calling IEventCatalogService
                    // - ConfigureJwtAuthentication ensures inbound JWT validation for
                    //   requests received by the ShoppingBasket service itself
                    // -------------------------------------------------------------------
                    _identityAuthority = GetIdentityAuthority();
                    var identitySection = Configuration.GetSection("Identity");                    
                    var ccEventCatalogSection = identitySection.GetSection("ClientCredentials:EventCatalog");

                    services.AddAccessTokenManagement(options =>
                    {
                        options.Client.Clients.Add("eventcatalog", new ClientCredentialsTokenRequest
                        {
                            Address = $"{_identityAuthority}/connect/token",
                            ClientId = ccEventCatalogSection["ClientId"],
                            ClientSecret = ccEventCatalogSection["ClientSecret"],
                            Scope = ccEventCatalogSection["Scope"]
                        });                             
                    });                    

                    ConfigureJwtAuthentication(services);

                    services.AddHttpClient<IEventCatalogService, EventCatalogService>(c =>
                        c.BaseAddress = new Uri(_eventCatalogUri))
                        .AddCorrelationIdForwarding()
                        .AddClientAccessTokenHandler("eventcatalog"); //For m2m communication

                    //The following are needed by the TokenExchangeService class for shopping basket -> order communication via the Azure Service Bus            
                    services.AddHttpContextAccessor();
                    services.AddScoped<TokenExchangeService>();                    
                    
                    break;
                }
                case "TrustGateway":
                {
                    // In TrustGateway mode, downstream APIs do not HAVE TO validate tokens since they are assumed to be protected at the infrastructure/network level.
                    // However, we're going to validate access tokens again at this level anyway to rehydrate our HttpContext.User object, which is used by our
                    // BasketsController.  The idea is defense in depth is better than using header-based user propagation

                    // -------------------------------------------------------------------
                    // TrustGateway Mode Configuration
                    //
                    // In this mode, downstream APIs (EventCatalog, Discount) are assumed to be
                    // protected at the infrastructure/network level, so user tokens are not strictly required.
                    // However, we still validate tokens here to rehydrate HttpContext.User for the
                    // BasketsController (defense in depth).
                    //
                    // AccessToken Management:
                    //   - Configures **client credentials** tokens for service-to-service (m2m) calls.
                    //   - "eventcatalog" client will acquire a token with scope "eventcatalog.read".
                    //   - "discount" client will acquire a token with scope "discount.fullaccess".
                    //
                    // HttpClient Setup:
                    //   - EventCatalogService uses the client credentials token for outgoing requests.
                    //   - Correlation IDs are forwarded automatically.
                    //
                    // TokenExchangeService:
                    //   - Required for shopping basket -> order service communication via Azure Service Bus.
                    //
                    // Key Points:
                    //   - This is **service-to-service communication** (client credentials flow).
                    //   - Different from the user token exchange used in AccessTokens mode.
                    // -------------------------------------------------------------------
                    _identityAuthority = GetIdentityAuthority();
                    var identitySection = Configuration.GetSection("Identity");                    
                    var ccEventCatalogSection = identitySection.GetSection("ClientCredentials:EventCatalog");
                    var ccDiscountSection = identitySection.GetSection("ClientCredentials:Discount");
                    services.AddAccessTokenManagement(options =>
                    {
                        // For ShoppingBasket -> EventCatalog service communication
                        options.Client.Clients.Add("eventcatalog", new ClientCredentialsTokenRequest
                        {
                            Address = $"{_identityAuthority}/connect/token",
                            ClientId = ccEventCatalogSection["ClientId"],
                            ClientSecret = ccEventCatalogSection["ClientSecret"],
                            Scope = ccEventCatalogSection["Scope"]
                        });

                        // For ShoppingBasket -> Discount service communication
                        options.Client.Clients.Add("discount", new ClientCredentialsTokenRequest
                        {
                            Address = $"{_identityAuthority}/connect/token",
                            ClientId = ccDiscountSection["ClientId"],
                            ClientSecret = ccDiscountSection["ClientSecret"],
                            Scope = ccDiscountSection["Scope"]
                        });
                    });
                    
                    ConfigureJwtAuthentication(services);

                    services.AddHttpClient<IEventCatalogService, EventCatalogService>(c =>
                        c.BaseAddress = new Uri(_eventCatalogUri))
                        .AddCorrelationIdForwarding() // <-- forwards the correlation ID automatically;
                        .AddClientAccessTokenHandler("eventcatalog");

                    //The following are needed by the TokenExchangeService class for shopping basket -> order communication via the Azure Service Bus            
                    services.AddHttpContextAccessor();
                    services.AddScoped<TokenExchangeService>();
                    
                    break;
                }
                case "None":
                default:
                    // No authentication — allow everything                    
                    services.AddHttpClient<IEventCatalogService, EventCatalogService>(c =>
                        c.BaseAddress = new Uri(_eventCatalogUri))
                        .AddCorrelationIdForwarding(); // <-- forwards the correlation ID automatically;
                    
                    break;
            }

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            // Cache Options are: "SQLite", "InMemory", "AzureRedis"
            var cacheMode = Configuration.GetValue<string>("ShoppingBasket:CacheMode");
            switch (cacheMode)
            {
                case "SQLite":
                    // EF Core with SQLite
                    // LocalDB will not work on Linux or MacOS so I'm going to use SQLite instead
                    // Uncomment the following line to use SQL Server on Windows instead of SQLite
                    // services.AddDbContext<ShoppingBasketDbContext>(options =>
                    // {
                    //     options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection_old"));
                    // });

                    services.AddDbContext<ShoppingBasketDbContext>(options =>
                        options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

                    // var optionsBuilder = new DbContextOptionsBuilder<ShoppingBasketDbContext>();
                    // optionsBuilder.UseSqlite(Configuration.GetConnectionString("DefaultConnection"));
                    // Old singleton registration (bad for DbContext lifetimes)
                    //services.AddSingleton(new BasketLinesIntegrationRepository(optionsBuilder.Options));

                    // services.AddScoped<BasketLinesIntegrationRepository>(sp =>
                    //     {
                    //         return new BasketLinesIntegrationRepository(optionsBuilder.Options);
                    //     });            
                    
                    services.AddScoped<BasketLinesIntegrationRepository>();
                    services.AddScoped<IBasketChangeEventRepository, BasketChangeEventRepository>();
                    services.AddScoped<IBasketRepository, SqliteBasketRepository>();
                    services.AddScoped<IBasketLinesRepository, SqliteBasketLinesRepository>();
                    services.AddScoped<IEventRepository, EventRepository>();
                    break;

                case "InMemory":
                    // In-memory distributed cache - Useful during development, or for single server deployments
                    services.AddDistributedMemoryCache();
                    services.AddScoped<IAzRedisBasketRepository>(sp =>
                    {
                        var cache = sp.GetRequiredService<IDistributedCache>();
                        var config = sp.GetRequiredService<IConfiguration>();
                        var logger = sp.GetRequiredService<ILogger<AzRedisBasketRepository>>();
                        var eventService = sp.GetRequiredService<IEventCatalogService>();
                        return new AzRedisBasketRepository(cache, config, logger, eventService);
                    });
                    
                    services.AddScoped<IBasketRepository, RedisBasketRepositoryAdapter>();
                    services.AddScoped<IBasketLinesRepository, RedisBasketRepositoryAdapter>();
                    services.AddScoped<IBasketChangeEventRepository, NoOpBasketChangeEventRepository>();
                    services.AddScoped<IAzRedisEventRepository, AzRedisEventRepository>();
                    services.AddScoped<IEventRepository, RedisEventRepositoryAdapter>();
                    services.AddHostedService<RedisEventSeeder>();
                    break;

                case "AzureRedis":
                    // Azure Cache for Redis implementation of IDistributedCache
                    // Requires creating the Resource in Azure and copying connection string settings into appsettings.json
                    services.AddStackExchangeRedisCache(options =>
                    {
                        options.Configuration = Configuration.GetConnectionString("RedisConnection");
                        options.InstanceName = "GloboTicket";
                        // optional configuration for cache
                        //options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
                        //{};
                    });

                    services.AddScoped<IAzRedisBasketRepository>(sp =>
                    {
                        var cache = sp.GetRequiredService<IDistributedCache>();
                        var config = sp.GetRequiredService<IConfiguration>();
                        var logger = sp.GetRequiredService<ILogger<AzRedisBasketRepository>>();
                        var eventService = sp.GetRequiredService<IEventCatalogService>();
                        return new AzRedisBasketRepository(cache, config, logger, eventService);
                    });

                    services.AddScoped<IBasketRepository, RedisBasketRepositoryAdapter>();
                    services.AddScoped<IBasketLinesRepository, RedisBasketRepositoryAdapter>();
                    services.AddScoped<IBasketChangeEventRepository, NoOpBasketChangeEventRepository>();
                    services.AddScoped<IAzRedisEventRepository, AzRedisEventRepository>();
                    services.AddScoped<IEventRepository, RedisEventRepositoryAdapter>();
                    services.AddHostedService<RedisEventSeeder>();

                    break;

                default:
                    throw new InvalidOperationException($"Unsupported CacheMode: {cacheMode}");
            }            

            services.AddHostedService<ServiceBusListener>();            

            //services.AddSingleton<IMessageBus, AzServiceBusMessageBus>();
            services.AddSingleton<IMessageBus>(sp =>
            {
                var connectionString = Configuration["ServiceBusConnectionString"] ?? throw new InvalidOperationException("ServiceBusConnectionString is not configured.");
                return new AzServiceBusMessageBus(connectionString);
            });

            // services.AddHttpClient<IEventCatalogService, EventCatalogService>(c =>
            //     c.BaseAddress = new Uri(Configuration["ApiConfigs:EventCatalog:Uri"]))
            //     .AddCorrelationIdForwarding(); // <-- forwards the correlation ID automatically;            

            bool useGrpc = Configuration.GetValue<bool>("ApiConfigs:Discount:UseGrpc");
            if (useGrpc)
            {
                // === gRPC approach ===
                services.AddGrpcClient<Discounts.DiscountsClient>(o =>
                    o.Address = new Uri(_discountUri));

                //services.AddScoped<IDiscountService, DiscountGrpcService>();

                // Register gRPC service with Polly wrapper
                services.AddScoped<IDiscountService>(sp =>
                {
                    var grpcClient = sp.GetRequiredService<Discounts.DiscountsClient>();
                    var retry = GetGrpcRetryPolicy();
                    var breaker = GetGrpcCircuitBreakerPolicy();
                    return new DiscountGrpcService(grpcClient, retry, breaker);
                });
            }
            else
            {
                services.AddHttpContextAccessor();
                services.AddTransient<AuthLoggingHandler>();
                services.AddHttpClient(); //Needed by DiscountRestService for creating the tokenClient via HttpClientFactory, which is used to get the exchange token

                // === REST approach ===
                var discountClientBuilder = services.AddHttpClient<IDiscountService, DiscountRestService>(c =>
                    c.BaseAddress = new Uri(_discountUri))
                    .AddPolicyHandler(GetRestRetryPolicy())
                    .AddPolicyHandler(GetRestCircuitBreakerPolicy())
                    .AddCorrelationIdForwarding() // <-- forwards the correlation ID automatically;
                    .AddHttpMessageHandler<AuthLoggingHandler>();

                if (authMode == "TrustGateway")
                {
                    // For m2m client credentials flow
                    discountClientBuilder.AddClientAccessTokenHandler("discount");
                }
            }            

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Shopping Basket API", Version = "v1" });
            });

            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddRedis(Configuration.GetConnectionString("RedisConnection"), name: "redis", timeout: TimeSpan.FromSeconds(3));
        }

        private void ConfigureJwtAuthentication(IServiceCollection services)
        {
            _jwtAuthority = GetJwtOptionsAuthority();
            _requireHttps = GetRequireHttps();

            var jwtOptions = Configuration.GetSection("JwtOptions");            
            var audience = jwtOptions["Audience"] ?? throw new InvalidOperationException("JwtOptions:Audience is not configured.");            
            var requiredScope = jwtOptions["RequiredScope"] ?? throw new InvalidOperationException("JwtOptions:RequiredScope is not configured.");

            // Allow only authenticated users to access our API Controllers and ensure properly scoped access token
            var requireAuthenticatedUserPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("scope", requiredScope)
                .Build();

            // Register global authorization filter
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUserPolicy));
            });

            // NOTE:  Prior to .NET 8.0, this was defined in JWT security token handler
            JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // Add JWT Bearer authentication
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = _jwtAuthority;
                    options.Audience = audience;
                    options.MapInboundClaims = false; // Preserve 'sub'
                    options.TokenValidationParameters = new()
                    {
                        NameClaimType = JwtClaimTypes.GivenName,
                        RoleClaimType = JwtClaimTypes.Role,
                        ValidTypes = new[] { "at+jwt" }
                    };
                    options.RequireHttpsMetadata = _requireHttps;
                });
                // .AddOAuth2Introspection(options =>
                        // {
                        //     options.Authority = authority;
                        //     options.ClientId = "imagegalleryapi";
                        //     options.ClientSecret = "apisecret";
                        //     options.NameClaimType = "given_name";
                        //     options.RoleClaimType = "role";
                        // });

            // Required when auth is ON - .AddAuthentication no longer implicitly calls in .net 8
            services.AddAuthorization();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            logger.LogInformation("ShoppingBasket → EventCatalog base URI resolved to: {Uri} (source: {Source})", _eventCatalogUri, _eventCatalogSource);
            logger.LogInformation("ShoppingBasket → Discount base URI resolved to: {Uri} (source: {Source})", _discountUri, _discountSource);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // -------------------------------------------------------------------------
                // GLOBAL EXCEPTION LOGGING FOR AKS / KUBERNETES
                //
                // In Production, ASP.NET Core does NOT automatically write unhandled exceptions to stdout or include them in responses.
                // This middleware:
                // 1. Logs full exception details to console → visible via `kubectl logs`
                // 2. Returns the exception in the HTTP response body → visible to clients
                //
                // Without this:
                // - API returns 500 with empty body
                // - No logs appear in Kubernetes
                // - Debugging is extremely difficult
                // With this:
                // - Full stack traces are visible
                // - Enables effective debugging in AKS
                // -------------------------------------------------------------------------
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                        var message = error?.ToString() ?? "Unknown error";

                        // Log to stdout (kubectl logs)
                        Console.WriteLine("UNHANDLED EXCEPTION:");
                        Console.WriteLine(message);

                        // Return full error to client (for debugging)
                        context.Response.StatusCode = 500;
                        context.Response.ContentType = "text/plain";

                        await context.Response.WriteAsync(message);
                    });
                });
            }

            app.UseCorrelationId();            

            // Middleware to log correlation ID for each incoming request and unhandled exceptions
            app.Use(async (context, next) =>
            {
                // Get correlation ID from the request header, or use the one generated by the middleware
                var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue)
                    ? headerValue.ToString()
                    : context.TraceIdentifier; // fallback if header missing

                // Add it to the logging scope so all logs include it
                var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();

                // Wrap in a logging scope for correlation ID
                using (logger.BeginScope("{CorrelationId}", correlationId))
                {
                    try
                    {
                        // LOG AUTH HEADER (TEMPORARY)
                        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                        {
                            logger.LogInformation("Incoming Authorization header: {AuthHeader}", authHeader.ToString());
                        }
                        else
                        {
                            logger.LogWarning("No Authorization header on incoming request");
                        }

                        logger.LogInformation("Incoming request {Path} with Correlation ID {CorrelationId}", context.Request.Path, correlationId);

                        // Call the next middleware / request handler    
                        await next();
                    }
                    catch (Exception ex)
                    {
                        // Log full exception with stack trace
                        logger.LogError(ex, "Unhandled exception processing request {Path} with Correlation ID {CorrelationId}", context.Request.Path, correlationId);
                        
                        // Rethrow so ASP.NET still returns 500
                        throw;
                    }
                }
            });

            app.UseHttpsRedirection();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shopping Basket API V1");

            });

            app.UseRouting();
            
            var authMode = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<string>("AuthenticationOptions:AuthMode");
            logger.LogInformation("ShoppingBasket AuthMode: {AuthMode}", authMode);
            
            if (authMode == "AccessTokens" || authMode == "TrustGateway")
            {
                logger.LogInformation("Identity authority resolved to: {Authority}", _identityAuthority);
                logger.LogInformation("JWT authority resolved to: {Authority} (RequireHttps: {RequireHttps})", _jwtAuthority, _requireHttps);
                app.UseAuthentication();                
            }
            else
            {
                logger.LogWarning("Authentication is disabled! All endpoints are anonymous.");
            }
            
            // This should always be called independent of authMode just in case you later add [Authorize] attributes on Controllers/Endpoints
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });
            });
        }

        private string GetIdentityAuthority()
        {
            // 1. Try container-specific env var
            var authority = Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY__SERVICE_URI");
            // 2. Fallback to default env var
            authority ??= Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY");
            // 3. Throw if nothing is set
            if (string.IsNullOrWhiteSpace(authority))
                throw new InvalidOperationException("No identity authority configured. Set IDENTITY__AUTHORITY__SERVICE_URI or IDENTITY__AUTHORITY environment variable.");

            return authority;
        }

        private string GetJwtOptionsAuthority()
        {
            // 1. Try container-specific env var
            return Environment.GetEnvironmentVariable("JWTOPTIONS__AUTHORITY__SERVICE_URI")
                // 2. Fallback to default env var
                ?? Environment.GetEnvironmentVariable("JWTOPTIONS__AUTHORITY")
                // 3. Throw if nothing is set
                ?? throw new InvalidOperationException("No JWT authority configured.  Set JWTOPTIONS__AUTHORITY__SERVICE_URI or JWTOPTIONS__AUTHORITY environment variable.");
        }

        private bool GetRequireHttps()
        {
            var requireHttpsStr = Environment.GetEnvironmentVariable("JWTOPTIONS__REQUIREHTTPSMETADATA") ?? "true";
            return bool.Parse(requireHttpsStr);
        }

        // === REST policies (HttpClient) ===
        private static IAsyncPolicy<HttpResponseMessage> GetRestRetryPolicy()
        {
            return HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(1.5, retryAttempt) * 1000),
                    onRetry: (_, waitingTime) =>
                    {
                        Console.WriteLine("Retrying REST due to transient error...");
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRestCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions.HandleTransientHttpError()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(15));
        }

        // === gRPC policies (RpcException) ===
        private static IAsyncPolicy GetGrpcRetryPolicy()
        {
            // return Policy
            //     .Handle<RpcException>(ex =>
            //         ex.StatusCode == StatusCode.Unavailable ||
            //         ex.StatusCode == StatusCode.DeadlineExceeded)
            //     .WaitAndRetryAsync(
            //         retryCount: 5,
            //         sleepDurationProvider: retryAttempt =>
            //             TimeSpan.FromMilliseconds(Math.Pow(1.5, retryAttempt) * 1000),
            //         onRetry: (ex, waitingTime) =>
            //         {
            //             Console.WriteLine($"Retrying gRPC due to {ex.Status}...");
            //         });

            return Policy
                .Handle<RpcException>(rpc =>
                    rpc.StatusCode == StatusCode.Unavailable ||
                    rpc.StatusCode == StatusCode.DeadlineExceeded)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: attempt =>
                        TimeSpan.FromMilliseconds(Math.Pow(1.5, attempt) * 1000),
                    onRetry: (exception, timespan) =>
                    {
                        if (exception is RpcException rpcEx)
                        {
                            Console.WriteLine(
                                $"Retrying gRPC in {timespan.TotalMilliseconds:N0} ms due to {rpcEx.StatusCode} ({rpcEx.Status.Detail})");
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Retrying gRPC in {timespan.TotalMilliseconds:N0} ms due to {exception.Message}");
                        }
                    });
        }

        private static IAsyncPolicy GetGrpcCircuitBreakerPolicy()
        {
            return Policy
                .Handle<RpcException>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(15));
        }
    }
}
