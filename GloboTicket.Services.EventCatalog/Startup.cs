using AutoMapper;
using GloboTicket.Services.EventCatalog.DbContexts;
using GloboTicket.Services.EventCatalog.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;
using GloboTicket.Integration.MessagingBus;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
//API Versioning
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using GloboTicket.Services.EventCatalog.Swagger;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using GloboTicket.Services.EventCatalog.HealthChecks;
using Microsoft.Azure.Cosmos;

namespace GloboTicket.Services.EventCatalog
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
            // LocalDB will not work on Linux or MacOS so I'm going to use SQLite instead
            // Uncomment the following line to use SQL Server on Windows instead of SQLite
            // services.AddDbContext<EventCatalogDbContext>(options =>
            //     options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection_old")));

            // services.AddDbContext<EventCatalogDbContext>(options =>
            //     options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            services.AddDbContext<EventCatalogCosmosDbContext>(options =>
                options.UseCosmos(Configuration["CosmosDb:Endpoint"].ToString(),
                Configuration["CosmosDb:Key"].ToString(),
                Configuration["CosmosDb:DatabaseName"].ToString())
            );

            services.AddScoped<ICategoryRepository, CategoryRepository>();
            //No longer needed after moving to the UoW pattern - UoW class is now responsible for creating & exposing EventRepository
            //services.AddScoped<IEventRepository, EventRepository>();

            // provides access to IEventRepository and IEventLogRepository
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            //services.AddSingleton<IMessageBus, AzServiceBusMessageBus>();
            services.AddSingleton<IMessageBus>(sp =>
            {
                var connectionString = Configuration["ServiceBusConnectionString"] ?? throw new InvalidOperationException("ServiceBusConnectionString is not configured.");
                return new AzServiceBusMessageBus(connectionString);
            });

            services.AddControllers();

            services.AddApiVersioning(o =>
            {
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0); //Expresses the default version number
                o.ReportApiVersions = true; //Adds api-supported-versions header to all responses to inform clients all versions availalbe
                // Example of combining/supporting multiple techniques - Use the below if you are NOT going to use URL segment versioning, which is configured below
                o.ApiVersionReader = ApiVersionReader.Combine(
                    new QueryStringApiVersionReader("api-version", "ver"),
                    new HeaderApiVersionReader("X-Version"),
                    new MediaTypeApiVersionReader("ver"));
            })
            .AddApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVV";           // e.g., v1, v1.1
                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;     // for URL segment versioning
            });
            
            var authMode = Configuration.GetValue<string>("AuthenticationOptions:AuthMode") ?? throw new InvalidOperationException("AuthenticationOptions:AuthMode is not configured.");

            // Allow only authenticated users to access our API Controllers for AccessTokens and TrustGateway modes - this means user tokens are required!
            var requireAuthenticatedUserPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();            

            switch (authMode) 
            {
                case "AccessTokens":
                {
                    _jwtAuthority = GetJwtOptionsAuthority();
                    _requireHttps = GetRequireHttps();
                    var jwtOptions = Configuration.GetSection("JwtOptions");                    
                    var audience = jwtOptions["Audience"] ?? throw new InvalidOperationException("JwtOptions:Audience is not configured.");                    
                    var readScope = jwtOptions["ReadScope"] ?? throw new InvalidOperationException("JwtOptions:ReadScope is not configured.");
                    var writeScope = jwtOptions["WriteScope"] ?? throw new InvalidOperationException("JwtOptions:WriteScope is not configured.");
                    // Required when auth is ON - .AddAuthentication no longer implicitly calls in .net 8
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("CanRead", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("scope", readScope);
                        });

                        options.AddPolicy("CanWrite", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("scope", writeScope);
                        });
                    });
                    // This means every controller/action requires an authenticated user.
                    // Equivalent to applying a global [Authorize] (no policy) attribute.
                    services.Configure<MvcOptions>(options =>
                    {
                        options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUserPolicy));
                    });

                    // NOTE:  Prior to .NET 8.0, this was defined in JWT security token handler
                    JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.Authority = _jwtAuthority;
                            //options.MetadataAddress = $"{_jwtAuthority}/.well-known/openid-configuration";
                            options.Audience = audience;
                            options.TokenValidationParameters = new()
                            {
                                NameClaimType = JwtClaimTypes.GivenName,
                                RoleClaimType = JwtClaimTypes.Role,
                                ValidTypes = new[] { "at+jwt" }
                            };
                            options.RequireHttpsMetadata = _requireHttps;                            
                            // Hook into events to log failures
                            options.Events = new JwtBearerEvents
                            {
                                OnAuthenticationFailed = context =>
                                {
                                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                    // Log full exception (will include fetch/validation failure)
                                    logger.LogError(context.Exception, "JWT Authentication failed while fetching keys from {Authority}", _jwtAuthority);
                                    return Task.CompletedTask;
                                },
                                OnTokenValidated = context =>
                                {
                                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                    logger.LogInformation("JWT token validated successfully for {sub}", 
                                        context.Principal?.FindFirst("sub")?.Value);
                                    return Task.CompletedTask;
                                },
                                OnMessageReceived = context =>
                                {
                                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                                    logger.LogInformation("Received token: {token}", 
                                        context.Token?.Substring(0, Math.Min(20, context.Token.Length)) + "...");
                                    return Task.CompletedTask;
                                }
                            };
                        });
                        // .AddOAuth2Introspection(options =>
                        // {
                        //     options.Authority = authority;
                        //     options.ClientId = "imagegalleryapi";
                        //     options.ClientSecret = "apisecret";
                        //     options.NameClaimType = "given_name";
                        //     options.RoleClaimType = "role";
                        // });                    
                    break;
                }

                case "TrustGateway":
                {
                    _jwtAuthority = GetJwtOptionsAuthority();
                    _requireHttps = GetRequireHttps();
                    var jwtOptions = Configuration.GetSection("JwtOptions");                    
                    var audience = jwtOptions["Audience"] ?? throw new InvalidOperationException("JwtOptions:Audience is not configured.");                    
                    var readScope = jwtOptions["ReadScope"] ?? throw new InvalidOperationException("JwtOptions:ReadScope is not configured.");
                    var writeScope = jwtOptions["WriteScope"] ?? throw new InvalidOperationException("JwtOptions:WriteScope is not configured.");
                    // Required when auth is ON - .AddAuthentication no longer implicitly calls in .net 8
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("CanRead", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("scope", readScope);
                        });

                        options.AddPolicy("CanWrite", policy =>
                        {
                            policy.RequireAuthenticatedUser();
                            policy.RequireClaim("scope", writeScope);
                        });
                    });    
                    // This means every controller/action requires an authenticated user.
                    // Equivalent to applying a global [Authorize] (no policy) attribute.
                    services.Configure<MvcOptions>(options =>
                    {
                        options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUserPolicy));
                    });

                    JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.Authority = _jwtAuthority;
                            options.Audience = audience;
                            options.TokenValidationParameters = new()
                            {
                                NameClaimType = JwtClaimTypes.GivenName,
                                RoleClaimType = JwtClaimTypes.Role,
                                ValidTypes = new[] { "at+jwt" }
                            };
                            options.RequireHttpsMetadata = _requireHttps;
                        });

                    break;
                }

                case "None":
                default:
                {
                    // No authorization/authentication — allow everything
                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("CanRead", policy => policy.RequireAssertion(_ => true));
                        options.AddPolicy("CanWrite", policy => policy.RequireAssertion(_ => true));
                    });
                    break;
                }
            }

            // OLD WAY before using Asp.Versioning.Mvc + Asp.Versioning.Mvc.ApiExplorer
            // services.AddSwaggerGen(c =>
            // {
            //     c.SwaggerDoc("v1", new OpenApiInfo { Title = "EventDto Catalog API", Version = "v1" });
            // });

            // NEW WAY: Swagger setup that works with Asp.Versioning.Mvc + Asp.Versioning.Mvc.ApiExplorer            
            // Registers a configurator that ASP.NET Core will run whenever SwaggerGenOptions are created
            // When someone needs SwaggerGenOptions, then run the ConfigureSwaggerOptions class to configure them.
            services.AddTransient<IConfigureOptions<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>, ConfigureSwaggerOptions>();
            // Registers Swagger and causes the Options pipeline to apply ConfigureSwaggerOptions
            // This will trigger the Configure method inside the ConfigureSwaggerOptions class
            services.AddSwaggerGen(options =>
                {
                    // add a custom operation filter which sets default values for versioned APIs                    
                    options.OperationFilter<SwaggerDefaultValues>();
                });

            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                return new CosmosClient(config["CosmosDb:Endpoint"], config["CosmosDb:Key"]);
            });
            //This is to simulate a code change
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddCheck<CosmosDbHealthCheck>("cosmos")
                .AddCheck("memory", () =>
                {
                    var allocated = GC.GetTotalMemory(false);
                    if (allocated > 200 * 1024 * 1024)
                    {
                        return HealthCheckResult.Unhealthy($"Memory usage too high: {allocated / 1024 / 1024} MB");
                    }
                    return HealthCheckResult.Healthy($"Memory OK: {allocated / 1024 / 1024} MB");
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider, ILogger<Startup> logger)
        {
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

            app.UseHttpsRedirection();

            app.UseSwagger();

            // app.UseSwaggerUI(c =>
            // {
            //     c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventDto Catalog API V1");
            // });
            
            app.UseSwaggerUI(options => {

                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
            });

            app.UseRouting();            

            var authMode = app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue<string>("AuthenticationOptions:AuthMode");
            logger.LogInformation("EventCatalog AuthMode: {AuthMode}", authMode);
            
            if (authMode == "AccessTokens" || authMode == "TrustGateway")
            {
                logger.LogInformation("JWT authority resolved to: {Authority} (RequireHttps: {RequireHttps})", _jwtAuthority, _requireHttps);
                app.UseAuthentication();

                app.Use(async (context, next) =>
                {
                    var user = context.User;
                    if (user?.Identity?.IsAuthenticated ?? false)
                    {
                        logger.LogInformation("User is authenticated");
                        foreach (var claim in user.Claims)
                        {
                            logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
                        }
                    }
                    else
                    {
                        logger.LogInformation("User is NOT authenticated");
                    }

                    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                    {
                        logger.LogInformation("Raw Authorization header: {Authorization}", authHeader.ToString());
                    }
                    else
                    {
                        logger.LogInformation("No Authorization header found in request");
                    }

                    await next();
                });
                //app.UseAuthorization();
            }
            else
            {
                logger.LogWarning("Authentication is disabled! All endpoints are anonymous.");
            }

            // This is always needed independent of AuthMode because the EventController is decorated with an [Authorize]
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
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
