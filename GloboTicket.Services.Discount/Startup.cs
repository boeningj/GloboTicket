using AutoMapper;
using GloboTicket.Services.Discount.DbContexts;
using GloboTicket.Services.Discount.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;

using CorrelationId;
using CorrelationId.DependencyInjection;   // for AddCorrelationId extension method
using CorrelationId.HttpClient;            // if you want HttpClient propagation
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace GloboTicket.Services.Discount
{
    public class Startup
    {
        private string _jwtAuthority;
        private bool _requireHttps;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDefaultCorrelationId(options =>
            {
                options.AddToLoggingScope = true;       // Include in ILogger scopes
                options.EnforceHeader = false;          // Don't require clients to send one
                options.IncludeInResponse = true;       // Returns ID in response headers
                options.RequestHeader = "X-Correlation-ID";
                options.ResponseHeader = "X-Correlation-ID";
            });
            
            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            services.AddDbContext<DiscountDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            // services.AddDbContext<DiscountDbContext>(options =>
            //     options.UseSqlite(Configuration.GetConnectionString("DefaultConnection_old")));

            services.AddScoped<ICouponRepository, CouponRepository>();

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            services.AddGrpc();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Discount API", Version = "v1" });
            });

            var authMode = Configuration.GetValue<string>("AuthenticationOptions:AuthMode") ?? throw new InvalidOperationException("AuthenticationOptions:AuthMode is not configured.");
            switch (authMode)
            {
                case "AccessTokens":
                {
                    // This mode is using Global-filter authorization instead of Policy-based authorization used below
                    // in TrustGateway mode.  Rule of Thumb:  Use global filter when all endpoints have the same auth rules.
                    // Use policies when you need fine-grained control (different claims, roles, or scopes for different endpoints)
                    _jwtAuthority = GetJwtOptionsAuthority();
                    _requireHttps = GetRequireHttps();
                    var jwtOptions = Configuration.GetSection("JwtOptions");                    
                    var audience = jwtOptions["Audience"] ?? throw new InvalidOperationException("JwtOptions:Audience is not configured.");                    
                    var requiredScope = jwtOptions["RequiredScope"] ?? throw new InvalidOperationException("JwtOptions:RequiredScope is not configured.");

                    var requireAuthenticatedUserPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .RequireClaim("scope", requiredScope)
                        .Build();

                    services.AddControllers(options =>
                    {
                        options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUserPolicy));
                    });                    

                    // Register the same named policy "DiscountService" since you have the controller decorated with 
                    // [Authorize(Policy = "DiscountService")]
                    // Alternatively, you could remove the Authorize attribute from the controller and then just call/uncomment:
                    //services.AddAuthorization();                    
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("DiscountService", requireAuthenticatedUserPolicy);
                    });
                    
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.Authority = _jwtAuthority;
                            options.Audience = audience;
                            options.MapInboundClaims = false;
                            options.RequireHttpsMetadata = _requireHttps;
                        });

                    break;
                }

                case "TrustGateway":
                {
                    // This mode is using Policy-based Authorization, which requires controllers to opt-in using the 
                    // [Authorize(Policy = "PolicyName")] attribute
                    
                    services.AddControllers();

                    _jwtAuthority = GetJwtOptionsAuthority();
                    _requireHttps = GetRequireHttps();
                    var jwtOptions = Configuration.GetSection("JwtOptions");                    
                    var audience = jwtOptions["Audience"] ?? throw new InvalidOperationException("JwtOptions:Audience is not configured.");                    
                    var requiredScope = jwtOptions["RequiredScope"] ?? throw new InvalidOperationException("JwtOptions:RequiredScope is not configured.");

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.Authority = _jwtAuthority;
                            options.Audience = audience;
                            options.MapInboundClaims = false;
                            options.RequireHttpsMetadata = _requireHttps;
                        });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("DiscountService", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("scope", requiredScope);
                        });
                    });

                    break;
                }

                case "None":
                default:
                {
                    services.AddControllers();
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("DiscountService", policy => policy.RequireAssertion(_ => true));
                    });
                    break;
                }
            }

            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy()) //always healthy (for liveness probe)
                .AddSqlServer(Configuration.GetConnectionString("DefaultConnection"), timeout: TimeSpan.FromSeconds(3)); //checks DB connectivity (for readiness probe)
            
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCorrelationId();

            // Middleware to log the correlation ID for each incoming request
            app.Use(async (context, next) =>
            {
                // Get correlation ID from the request header, or use the one generated by the middleware
                var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var headerValue)
                    ? headerValue.ToString()
                    : context.TraceIdentifier; // fallback if header missing

                // Add it to the logging scope so all logs include it
                var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
                using (logger.BeginScope("{CorrelationId}", correlationId))
                {
                    logger.LogInformation("Incoming request {Path} with Correlation ID {CorrelationId}", context.Request.Path, correlationId);

                    await next();
                }
            });            

            app.UseHttpsRedirection();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Discount API V1");

            });

            app.UseRouting();

            var authMode = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<string>("AuthenticationOptions:AuthMode");
            logger.LogInformation("Discount AuthMode: {AuthMode}", authMode);
            
            if (authMode == "AccessTokens" || authMode == "TrustGateway")
            {
                logger.LogInformation("JWT authority resolved to: {Authority} (RequireHttps: {RequireHttps})", _jwtAuthority, _requireHttps);
                app.UseAuthentication();
                //app.UseAuthorization();
            }
            else
            {
                logger.LogWarning("Authentication is disabled! All endpoints are anonymous.");
            }
            // This is always needed independent of AuthMode because the DiscountController is decorated with an [Authorize]
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGrpcService<Services.DiscountsService>();
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Name == "self" });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name != "self" });
            });
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
    }
}
