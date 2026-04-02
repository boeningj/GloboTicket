using GloboTicket.Services.ShoppingBasket.Extensions;
using GloboTicket.Services.ShoppingBasket.Models;
using System;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http; // For IHttpContextAccessor
using IdentityModel.Client; // For RequestTokenAsync, TokenRequest, & SetBearerToken
using Microsoft.AspNetCore.Authentication; // For extension methods GetTokenAsync

namespace GloboTicket.Services.ShoppingBasket.Services
{
    public class DiscountRestService : IDiscountService
    {
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;
        private string _cachedToken;
        private DateTime _cachedTokenExpiration;
        private string _identityAuthority;

        public DiscountRestService(HttpClient client, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            _client = client;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
            _identityAuthority = GetIdentityAuthority(); 
        }

        public async Task<Coupon> GetCoupon(Guid userId)
        {
            await AttachAccessTokenAsync();

            var response = await _client.GetAsync($"/api/discount/user/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.ReadContentAs<Coupon>();
        }

        public async Task<Coupon> GetCouponWithError(Guid userId)
        {
            await AttachAccessTokenAsync();

            var response = await _client.GetAsync($"/api/discount/error/{userId}");
            return await response.ReadContentAs<Coupon>();
        }

        // -------------------------------------------------------------------
        // AttachAccessTokenAsync
        //
        // This method is used strictly for **ShoppingBasket -> Discount service calls**.
        // In AccessTokens mode, it:
        //   1. Retrieves the incoming user's access token from the HTTP context.
        //   2. Uses OAuth 2.0 Token Exchange to request a **downstream token**
        //      for the Discount service with the required scope (`discount.fullaccess`).
        //   3. Caches the exchanged token in memory to avoid repeated calls.
        //   4. Attaches the token to the outgoing HttpClient request.
        //
        // Key points:
        //   - This is **user-to-service propagation** (the user token is exchanged for a service-specific token).
        //   - Only runs in AccessTokens mode; in TrustGateway mode, the gateway handles user propagation.
        //   - This is **different from client credentials m2m tokens** used internally to call EventCatalog.
        // -------------------------------------------------------------------
        private async Task AttachAccessTokenAsync()
        {
            var authMode = _configuration.GetValue<string>("AuthenticationOptions:AuthMode");
            if (authMode != "AccessTokens")
            {
                return; // no auth, or different mode
            }

            // Get the incoming user token
            var userToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(userToken))
            {
                throw new InvalidOperationException("No user access token found in HttpContext.");
            }

            // Use cached token if valid
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _cachedTokenExpiration)
            {
                _client.SetBearerToken(_cachedToken);
                return;
            }

            // Otherwise call token exchange endpoint            
            var tokenExchangeSection = _configuration.GetSection("Identity:TokenExchange");
            using var tokenClient = _httpClientFactory.CreateClient();
            var tokenResponse = await tokenClient.RequestTokenAsync(new TokenRequest
            {
                Address = $"{_identityAuthority}/connect/token",
                GrantType = "urn:ietf:params:oauth:grant-type:token-exchange",
                ClientId = tokenExchangeSection["ClientId"],
                ClientSecret = tokenExchangeSection["ClientSecret"],

                Parameters =
                {
                    { "subject_token", userToken },
                    { "subject_token_type", "urn:ietf:params:oauth:token-type:access_token" },
                    { "scope", tokenExchangeSection["Scopes:Discount"] }
                }
            });

            if (tokenResponse.IsError)
            {             
                // Include ErrorDescription (if available) for the full message
                var details = tokenResponse.ErrorDescription ?? tokenResponse.Raw; // full raw JSON if ErrorDescription is missing
                throw new Exception($"Token exchange failed: {tokenResponse.Error}. Details: {details}");                
            }

            // Cache exchanged token and expiration (minus a few seconds buffer)
            _cachedToken = tokenResponse.AccessToken;
            _cachedTokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 10);

            // Attach new exchanged token
            _client.SetBearerToken(tokenResponse.AccessToken);
        }

        private static string GetIdentityAuthority()
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
    }
}