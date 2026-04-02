using IdentityModel.AspNetCore.AccessTokenManagement;
using IdentityModel.Client;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

// Out of all the code I've written for this project, this piece is probably my favoriate and also my best.
// I've learned so much from it.

namespace GloboTicket.Gateway.DelegatingHandlers
{
    public class EventCatalogTokenExchangeHandler : DelegatingHandler
    {
        // For TokenExchange or any outbound API calls
        private readonly IHttpClientFactory _httpClientFactory;
        
        //IdentityModel.AspNetCore - used to cache the exchanged token until it expires to reduce unnecessary requests to the token endpoint
        private readonly IClientAccessTokenCache _clientAccessTokenCache;
        
        // For fetching the discovery document
        private static readonly HttpClient _staticClient = new();

        // Cache the discovery document response.  Lazy ensures only one discovery request is ever started
        // The lambda reads the environment variable at runtime, falling back to localhost if it isn’t set
        // IMPORTANT NOTE:
        // Ocelot constructs a separate DelegatingHandler instance for each configured route.
        // To avoid fetching the IdentityServer discovery document multiple times,
        // we cache it in a static Lazy so it is initialized once per process, not per handler.
        //
        // Because this field is static, it cannot access instance-scoped dependencies
        // like IConfiguration injected into the handler. For that reason we resolve the
        // authority and HTTPS settings from environment variables instead of _config.
        private static readonly Lazy<Task<DiscoveryDocumentResponse>> _discovery =
            new(() =>
            {
                var authority = Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY__SERVICE_URI") //http://globoticket-idp:5020
                    ?? Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY") // Fallback IdentityServer authority for host callers:
                    //   host mode  → https://localhost:5020
                    //   Docker IDP → https://localhost:5021
                    // Container callers should use IDENTITY__AUTHORITY__SERVICE_URI instead.
                    ?? throw new InvalidOperationException("No identity authority configured. Set IDENTITY__AUTHORITY__SERVICE_URI or IDENTITY__AUTHORITY environment variable.");

                var requireHttps = bool.Parse(Environment.GetEnvironmentVariable("JWTOPTIONS__REQUIREHTTPSMETADATA") ?? "true");

                return _staticClient.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
                {
                    Address = authority,
                    Policy = { RequireHttps = requireHttps }
                });
            });        

        // Prevent multiple simultaneous token exchanges for the same scope. 
        // Different scopes can exchange in parallel (e.g. read vs write tokens).
        // ConcurrentDictionary is necessary if you have multiple scopes and want token exchanges for different scopes to run in parallel without blocking each other.
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _scopeSemaphores = new();

        private readonly ILogger<EventCatalogTokenExchangeHandler> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _readScope;
        private readonly string _writeScope;

        public EventCatalogTokenExchangeHandler(IHttpClientFactory httpClientFactory,
            IClientAccessTokenCache clientAccessTokenCache, ILogger<EventCatalogTokenExchangeHandler> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _clientAccessTokenCache = clientAccessTokenCache;
            _logger = logger;

            var tokenExchangeSection = configuration.GetSection("Identity:TokenExchange");
            _clientId = tokenExchangeSection["ClientId"] ?? throw new InvalidOperationException($"{nameof(EventCatalogTokenExchangeHandler)}: TokenExchange ClientId not configured.");
            _clientSecret = tokenExchangeSection["ClientSecret"] ?? throw new InvalidOperationException($"{nameof(EventCatalogTokenExchangeHandler)}: TokenExchange ClientSecret not configured.");

            var scopes = configuration.GetSection("Identity:TokenExchange:Scopes");
            _readScope = scopes["EventCatalogRead"] ?? throw new InvalidOperationException($"{nameof(EventCatalogTokenExchangeHandler)}: EventCatalogRead scope not configured.");
            _writeScope = scopes["EventCatalogWrite"] ?? throw new InvalidOperationException($"{nameof(EventCatalogTokenExchangeHandler)}: EventCatalogWrite scope not configured.");

            _logger.LogInformation("Created {Handler} instance. HashCode={HashCode}", nameof(EventCatalogTokenExchangeHandler), GetHashCode());
        }        

        // Responsible for returning a non expired access token
        public async Task<string> GetAccessTokenAsync(string incomingToken, string scope, CancellationToken cancellationToken)
        {            
            // cacheKey must include scope since it changes the access token
            var cacheKey = $"gatewaytodownstreamtokenexchangeclient:eventcatalog:{scope}";
            var semaphore = GetSemaphoreForScope(scope);

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // NOTE:  GetAsync will only return an access token if it hasn't expired yet.  Null means there's either no access token or it has expired
                var item = await _clientAccessTokenCache.GetAsync(cacheKey, new ClientAccessTokenParameters(), cancellationToken);
                if (item != null)
                {
                     _logger.LogInformation("Token returned from cache for scope '{scope}'", cacheKey);
                    return item.AccessToken!;
                }

                // If we get here, token wasn't in cache
                _logger.LogInformation("Token NOT in cache, requesting new token for scope '{scope}'", cacheKey);

                var (accessToken, expiresIn) = await ExchangeTokenAsync(incomingToken, scope, cancellationToken);

                await _clientAccessTokenCache.SetAsync(cacheKey, accessToken, expiresIn, new ClientAccessTokenParameters(), cancellationToken);

                _logger.LogInformation("New token fetched and cached for scope '{scope}'", cacheKey);

                return accessToken;
            }
            finally
            {
                semaphore.Release();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Extract the current token
            var incomingToken = request.Headers.Authorization?.Parameter;

            if (string.IsNullOrEmpty(incomingToken))
            {
                // No token was provided, handle it appropriately.
                // throw (or just skip token exchange)
                throw new InvalidOperationException("No bearer token found in the incoming request.");
            }

            var scope = GetScopeFromHttpRequestMethod(request);

            // Exchange it
            //var newToken = await ExchangeToken(incomingToken, scope);            

            // Replace the incoming bearer token with our new one
            //request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);

            // Set the bearer token
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(incomingToken, scope, cancellationToken));

            return await base.SendAsync(request, cancellationToken);
        }

        // Use one semaphore for each requested scope
        private SemaphoreSlim GetSemaphoreForScope(string scope) => _scopeSemaphores.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));

        private async Task<(string accessToken, int expiresIn)> ExchangeTokenAsync(string incomingToken, string scope, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();

            // Use cached discovery document
            var discoveryDocumentResponse = await _discovery.Value;
            if (discoveryDocumentResponse.IsError)
            {
                throw new Exception(discoveryDocumentResponse.Error);
            }

            var customParams = new Parameters
            {
                { "subject_token_type", "urn:ietf:params:oauth:token-type:access_token" },
                { "subject_token", incomingToken },
                { "scope", scope}
                //{ "scope", "openid profile eventcatalog.read eventcatalog.write" }
            };

            var tokenResponse = await client.RequestTokenAsync(new TokenRequest()
            {
                Address = discoveryDocumentResponse.TokenEndpoint,
                GrantType = "urn:ietf:params:oauth:grant-type:token-exchange",
                Parameters = customParams,
                ClientId = _clientId,
                ClientSecret = _clientSecret
            },
            cancellationToken);

            if (tokenResponse.IsError)
            {
                // Include ErrorDescription (if available) for the full message
                var details = tokenResponse.ErrorDescription ?? tokenResponse.Raw; // full raw JSON if ErrorDescription is missing
                throw new Exception($"Token exchange failed: {tokenResponse.Error}. Details: {details}");
            }

            return (tokenResponse.AccessToken, tokenResponse.ExpiresIn);

        }

        private string GetScopeFromHttpRequestMethod(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Get)
            {
                return _readScope; //"openid profile eventcatalog.read";
            }

            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put || request.Method == HttpMethod.Delete)
            {
                return _writeScope; //"openid profile eventcatalog.write";
            }

            throw new InvalidOperationException($"Unsupported HTTP method {request.Method}");
        }
    }
}