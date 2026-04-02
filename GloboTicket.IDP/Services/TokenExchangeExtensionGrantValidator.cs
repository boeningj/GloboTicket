using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

// When any client sends a request to the token endpoint with grant_type=urn:ietf:params:oauth:grant-type:token-exchange, this validator runs.
public class TokenExchangeExtensionGrantValidator : IExtensionGrantValidator
{
    public string GrantType => "urn:ietf:params:oauth:grant-type:token-exchange";
    private string _accessTokenType => "urn:ietf:params:oauth:token-type:access_token";
    private readonly ITokenValidator _validator; //IdentityServer build in token validator
    private readonly ILogger<TokenExchangeExtensionGrantValidator> _logger;
    private readonly IProfileService _profileService;

    public TokenExchangeExtensionGrantValidator(
        ITokenValidator validator,
        IProfileService profileService,
        ILogger<TokenExchangeExtensionGrantValidator> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }    

    public async Task ValidateAsync(ExtensionGrantValidationContext context)
    {
        // Make sure the grant_type is valid by matching it to this.GrantType
        var requestedGrant = context.Request.Raw.Get("grant_type");
        if (requestedGrant != GrantType)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Invalid grant type.");
            return;
        }

        // Make sure we received a subject token
        var subjectToken = context.Request.Raw.Get("subject_token");
        if (string.IsNullOrWhiteSpace(subjectToken))
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Subject token missing.");
            return;
        }

        // Make sure we received a subject token type that matches this._accessTokenType
        var subjectTokenType = context.Request.Raw.Get("subject_token_type");
        if (subjectTokenType != _accessTokenType)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Invalid subject token type.");
            return;
        }

        // Validate the incoming access token, which is our subject token received in the request
        var validationResult = await _validator.ValidateAccessTokenAsync(subjectToken);
        if (validationResult.IsError)
        {
            _logger.LogWarning("Token exchange failed: subject token invalid.");
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "Subject token invalid.");
            return;
        }

        // Get the subject "sub" from the access token to use in the new exchanged token
        var subjectClaim = (validationResult.Claims ?? Enumerable.Empty<Claim>()).FirstOrDefault(c => c.Type == "sub");
        if (subjectClaim == null)
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "Subject token must contain 'sub' claim.");
            return;
        }

        // Optional: filter/transform claims for the new token
        // Only keep the newClaims manipulation if you need to add custom claims beyond what IdentityServer normally includes.
        // Otherwise, it’s just extra code that isn’t used for API access tokens.
        // var newClaims = (validationResult.Claims ?? Enumerable.Empty<Claim>())
        //     .Where(c => c.Type != "aud")  // remove original audience
        //     .ToList();
        // newClaims.Add(new Claim("aud", "discount"));            

        // Optional: assign scopes requested for the target API
        var allowedScopes = new[] { "discount.fullaccess", "eventcatalog.read", "eventcatalog.write", "shoppingbasket.fullaccess", "ordering.fullaccess" }; //don't just accept any scope that the client asks for.
        //this prevents clients from requesting "admin.fullaccess" and escalating prviliges via token exchange
        var requestedScopes = context.Request.Raw.Get("scope")?
            .Split(' ')
            .Where(s => allowedScopes.Contains(s))
            .ToList() ?? new List<string>();
        
        if (!requestedScopes.Any())
        {
            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidRequest, "TokenExchangeExtensionGrantValidator - Requested scope(s) are not allowed.");
            return;
        }
        context.Request.RequestedScopes = requestedScopes;

        // Duende IdentityServer uses RequestedScopes when creating the outgoing access token.  Without the above line
        // the exchanged token would NOT contain any scopes (or would inherit defaults).

        // we've got the subject claim and the token is valid. This is where additional rules
        // can be checked that can result in not giving access, for example: checking if the user
        // is allowed access to the API (s)he wants access to.  This depends on the implementation.  
        //
        // ... additional checks
        //
        // This is also where claims transformation can happen, additional claims about the 
        // user can be returned, etc
        //
        // ... claims transformation
        // 
        // If all checks out we set the result to a GrantValidationResult, passing in 
        // the users' identifier, authentication method ("access token") 
        // and set of claims (in this example: the incoming claims)
        //
        // It's these incoming claims that will be checked (by IdSrv) against the approved
        // api scope (=> claims) rules.  Eg: if the incoming token has an email claim but the 
        // api scope that's requested doesn't offer access to that email claim, it will not be included.  
        // 
        // This also works the other way around: it's only the incoming claims passed through to the
        // grant validation result that are considered.  Eg: if an incoming token doesn't have an email claim 
        // and the email claim is thus not passed to the grant validation result, the new token
        // will not contain an email claim even if the requested api scope grants access to it.

        context.Result = new GrantValidationResult(subjectClaim.Value, "access_token"); //, newClaims);
    }
}