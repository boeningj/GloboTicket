using IdentityModel.AspNetCore.AccessTokenManagement;
using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading;

namespace GloboTicket.Services.ShoppingBasket.Helpers
{
    public class TokenExchangeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
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

        // NOTE:  ConcurrentDictionary is necessary if you have multiple scopes and want token exchanges for different scopes
        // to run in parallel without blocking each other.  Refer to GloboTicket.Gateway.Ocelot.Handlers.EventCatalogTokenExchangeHandler
        private static readonly SemaphoreSlim _tokenSemaphore = new(1, 1);

        private readonly ILogger<TokenExchangeService> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public TokenExchangeService(IHttpClientFactory httpClientFactory,
            IClientAccessTokenCache clientAccessTokenCache, ILogger<TokenExchangeService> logger, IConfiguration configuration)
        {
            _clientAccessTokenCache = clientAccessTokenCache;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            
            var tokenExchangeSection = configuration.GetSection("Identity:TokenExchange");
            _clientId = tokenExchangeSection["ClientId"] ?? throw new InvalidOperationException($"{nameof(TokenExchangeService)}: TokenExchange ClientId not configured.");
            _clientSecret = tokenExchangeSection["ClientSecret"] ?? throw new InvalidOperationException($"{nameof(TokenExchangeService)}: TokenExchange ClientSecret not configured.");

            _logger.LogInformation("Created TokenExchangeService instance. HashCode={HashCode}", GetHashCode());            
        }

        public async Task<string> GetTokenAsync(string incomingToken, string apiScope, CancellationToken cancellationToken)
        {
            var cacheKey = $"shoppingbaskettodownstreamtokenexchangeclient:{apiScope}";

            // If the "gate" is open -> then allow code to go through otherwise wait asynchronously
            await _tokenSemaphore.WaitAsync(cancellationToken);
            try
            {
                var item = await _clientAccessTokenCache.GetAsync(cacheKey, new ClientAccessTokenParameters(), cancellationToken);
                if (item != null)
                {
                    _logger.LogInformation("Token returned from cache for scope '{scope}'", apiScope);
                    return item.AccessToken;
                }
                
                // If we get here, token wasn't in cache
                _logger.LogInformation("Token NOT in cache, requesting new token for scope '{scope}'", apiScope);

                // Use cached discovery document.  NOTE:  This doesn’t respect cancellation yet.
                var discoveryDocumentResponse = await _discovery.Value;
                if (discoveryDocumentResponse.IsError)
                {
                    throw new Exception(discoveryDocumentResponse.Error);
                }            

                var customParams = new Parameters
                {
                    { "subject_token_type", "urn:ietf:params:oauth:token-type:access_token"},
                    { "subject_token", incomingToken},
                    { "scope", $"openid profile {apiScope}" }            
                };
                
                var client = _httpClientFactory.CreateClient();
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

                await _clientAccessTokenCache.SetAsync(cacheKey, tokenResponse.AccessToken, tokenResponse.ExpiresIn, new ClientAccessTokenParameters(), cancellationToken);

                _logger.LogInformation("New token fetched and cached for scope '{scope}'", apiScope);

                return tokenResponse.AccessToken;
            }
            finally
            {
                // Open the "gate"
                _tokenSemaphore.Release();
            }
        }
    }
}