using IdentityModel;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace GloboTicket.Services.Ordering.Helpers
{
    public class TokenValidationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Lazy<Task<DiscoveryDocumentResponse>> _discovery;
        private readonly ILogger<TokenValidationService> _logger;
        private readonly string _validIssuer;
        private readonly string _validAudience;
        private readonly string _requiredScope;

        public TokenValidationService(IHttpClientFactory httpClientFactory, ILogger<TokenValidationService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var authority = GetIdentityAuthority();
            var requireHttps = GetRequireHttps();

            _discovery = new Lazy<Task<DiscoveryDocumentResponse>>(async () =>
            {
                _logger.LogInformation("Fetching discovery document for TokenValidationService from IdentityServer...");
                var client = _httpClientFactory.CreateClient();
                var request = new DiscoveryDocumentRequest
                {
                    Address = authority,
                    Policy = { RequireHttps = requireHttps }
                };

                var response = await client.GetDiscoveryDocumentAsync(request);
                if (response.IsError) throw new Exception(response.Error);
                return response;
            });

            //IDP Server
            _validIssuer = authority;
            //ordering
            _validAudience = configuration["Identity:TokenValidation:Audience"] ?? throw new InvalidOperationException("TokenValidationService: Valid Audience not configured.");
            //ordering.fullaccess
            _requiredScope = configuration["Identity:TokenValidation:RequiredScope"] ?? throw new InvalidOperationException("TokenValidationService: RequiredScope not configured.");
        }

        public async Task<bool> ValidateTokenAsync(string tokenToValidate, DateTimeOffset receivedAt)
        {
            //receivedAt is the time the message was received by the message bus
            _logger.LogInformation("Validating token received at {ReceivedAt} (UTC: {ReceivedAtUtc})", receivedAt, receivedAt.UtcDateTime);

            var discoveryDocumentResponse = await _discovery.Value;

            try
            {
                //Extract the issuer signing keys from the discovery document
                /// <summary>
                /// Builds a list of RSA signing keys from the IdentityServer discovery document.
                /// These keys are used to validate the signature of incoming JWT access tokens.
                /// Each key contains a modulus and exponent (from the JWKS), wrapped in an RsaSecurityKey.
                /// The KeyId (kid) ensures the validator selects the correct key if multiple keys exist.
                /// </summary>
                var issuerSigningKeys = new List<SecurityKey>();
                foreach (var webKey in discoveryDocumentResponse.KeySet.Keys) // JSON Web Key Set (JWKS)
                {
                    var e = Base64Url.Decode(webKey.E);
                    var n = Base64Url.Decode(webKey.N);

                    var key = new RsaSecurityKey(new RSAParameters
                    { Exponent = e, Modulus = n })
                    {
                        KeyId = webKey.Kid
                    };

                    issuerSigningKeys.Add(key);
                }

                //Linq way:
                /*
                IEnumerable<SecurityKey> issuerSigningKeys2 = discoveryDocumentResponse.KeySet.Keys
                    .Select(webKey =>
                    {
                        var e = Base64Url.Decode(webKey.E);
                        var n = Base64Url.Decode(webKey.N);

                        return new RsaSecurityKey(new RSAParameters
                        {
                            Exponent = e,
                            Modulus = n
                        })
                        {
                            KeyId = webKey.Kid
                        };
                    });
                */

                _logger.LogDebug("Loaded {KeyCount} signing keys from discovery document", issuerSigningKeys.Count);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = _validAudience,
                    ValidIssuer = _validIssuer, //JWT iss claim
                    IssuerSigningKeys = issuerSigningKeys, // IEnumerable<SecurityKey>

                    // Prevent replay attacks by ensuring the token was still valid
                    // at the moment the message was enqueued by Azure Service Bus
                    LifetimeValidator = (notBefore, expires, securityToken, parameters) =>
                    {
                        return expires.HasValue && expires.Value > receivedAt.UtcDateTime; //The token must not have been expired when the message was placed on the bus.
                    }
                };

                //NOTE:  The ValidateToken() call only validates the token's signature, issuer, audience, and lifetime
                var claimsPrincipal = new JwtSecurityTokenHandler().ValidateToken(tokenToValidate, tokenValidationParameters, out var rawValidatedToken);

                //Validate the token's scope as well
                var scopeClaims = claimsPrincipal.FindAll("scope").Select(c => c.Value);
                if (!scopeClaims.Contains(_requiredScope))
                {                    
                    _logger.LogWarning("Token is missing required scope '{RequiredScope}'. Scope claims: {ScopeClaims}", _requiredScope, string.Join(", ", scopeClaims));
                    return false;
                }
                
                _logger.LogInformation("Token successfully validated for required scope '{RequiredScope}'", _requiredScope);
                return true;
            }
            catch (SecurityTokenValidationException ex)
            {
                _logger.LogWarning(ex, "Token validation failed due to signature, issuer, audience, or lifetime");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return false;
            }
        }

        private static string GetIdentityAuthority()
        {
            // 1. Try container-specific env var
            var authority = Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY__SERVICE_URI");
            // 2. Fallback to default env var
            authority ??= Environment.GetEnvironmentVariable("IDENTITY__AUTHORITY");
            // 3. Throw if nothing is set
            if (string.IsNullOrWhiteSpace(authority))
                throw new InvalidOperationException("TokenValidationService: Identity Authority not configured.  Set IDENTITY__AUTHORITY__SERVICE_URI or IDENTITY__AUTHORITY environment variable.");

            return authority;
        }

        private bool GetRequireHttps()
        {
            var requireHttpsStr = Environment.GetEnvironmentVariable("JWTOPTIONS__REQUIREHTTPSMETADATA") ?? "true";
            return bool.Parse(requireHttpsStr);
        }
    }
}