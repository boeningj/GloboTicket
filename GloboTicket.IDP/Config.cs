using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace GloboTicket.IDP;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
             new IdentityResource[]
             {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            // new IdentityResource("roles",
            //     "Your role(s)",
            //     new [] { "role" }),
            // new IdentityResource("country",
            //     "The country you're living in",
            //     new List<string>() { "country" })
             };

    public static IEnumerable<ApiResource> ApiResources =>
     new ApiResource[]
         {
            new ApiResource("eventcatalog", "Event catalog API")
            {
                Scopes = { "eventcatalog.read", "eventcatalog.write" }
            },
            new ApiResource("shoppingbasket", "Shopping basket API")
            {
                Scopes = { "shoppingbasket.fullaccess" }
            },
            new ApiResource("discount", "Discount API")
            {
                Scopes = { "discount.fullaccess" }
            },
            new ApiResource("globoticketgateway", "GloboTicket Gateway")
            {
                Scopes = { "globoticketgateway.fullaccess" }
            },
            new ApiResource("ordering", "Ordering API")
            {
                Scopes = { "ordering.fullaccess"}
            }
            // new ApiResource("globoticket", "GlobotTicket APIs")
            //   {
            //       Scopes = { "globoticket.fullaccess" }
            //   }
            //  new ApiResource("imagegalleryapi",
            //      "Image Gallery API",
            //      new [] { "role", "country" })
            //  {
            //      Scopes = { "imagegalleryapi.fullaccess",
            //          "imagegalleryapi.read",
            //          "imagegalleryapi.write"},
            //     ApiSecrets = { new Secret("apisecret".Sha256()) }
            //  }
         };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
            {
                new ApiScope("shoppingbasket.fullaccess"),
                new ApiScope("discount.fullaccess"),
                new ApiScope("eventcatalog.read"),
                new ApiScope("eventcatalog.write"),
                new ApiScope("globoticketgateway.fullaccess"),
                new ApiScope("ordering.fullaccess")
            };

    /*
    public static IEnumerable<Client> Clients =>
        new Client[]
            {
                new Client
                {
                    ClientName = "GloboTicket Machine 2 Machine Client",
                    ClientId = "globoticketm2m",
                    ClientSecrets = { new Secret("eac7008f-1b35-4325-ac8d-4a71932e6088".Sha256()) },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = { "eventcatalog.read", "discount.fullaccess" }
                },
                new Client
                {
                    ClientName = "GloboTicket Interactive Client",
                    ClientId = "globoticketinteractive", // Direct to service MVC Client
                    ClientSecrets = { new Secret("ce766e16-df99-411d-8d31-0f5bbc6b8eba".Sha256()) },
                    AllowedGrantTypes = GrantTypes.Code,
                    RedirectUris = { "https://localhost:5000/signin-oidc" },
                    PostLogoutRedirectUris = { "https://localhost:5000/signout-callback-oidc" },
                    AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "globoticketgateway.fullaccess", "shoppingbasket.fullaccess", "eventcatalog.read", "eventcatalog.write" }
                },
                new Client
                {
                    ClientName = "GloboTicket Interactive Gateway Client",
                    ClientId = "globoticketinteractivegateway", // Ocelot API Gateway
                    ClientSecrets = { new Secret("c9b4c2e3-8c98-4a91-8a94-ff15fd41db91".Sha256()) },
                    AllowedGrantTypes = GrantTypes.Code,
                    RedirectUris = { "https://localhost:5000/signin-oidc" },
                    PostLogoutRedirectUris = { "https://localhost:5000/signout-callback-oidc" },
                    //AllowOfflineAccess = true,
                    AllowedScopes = { "openid", "profile", "globoticketgateway.fullaccess" }
                },
                new Client
                {
                    //This client combines the two clients above (globoticketm2m & globoticketinteractive) into one single client that can
                    // get user tokens and client credentials for m2m.  NOTE - I am NOT using this client (yet)
                    ClientName = "GloboTicket Client",
                    ClientId = "globoticket",
                    ClientSecrets = { new Secret("ce766e16-df99-411d-8d31-0f5bbc6b8eba".Sha256()) },
                    AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                    RedirectUris = { "https://localhost:5000/signin-oidc" },
                    PostLogoutRedirectUris = { "https://localhost:5000/signout-callback-oidc" },
                    AllowedScopes = { "openid", "profile", "shoppingbasket.fullaccess", "eventcatalog.read", "eventcatalog.write" }
                },
                new Client
                {
                    ClientId = "shoppingbaskettodownstreamtokenexchangeclient",
                    ClientName = "Shopping Basket Token Exchange Client",
                    AllowedGrantTypes = new[] { "urn:ietf:params:oauth:grant-type:token-exchange" },
                    ClientSecrets = { new Secret("0cdea0bc-779e-4368-b46b-09956f70712c".Sha256()) },
                    AllowedScopes = { "openid", "profile", "discount.fullaccess", "ordering.fullaccess" }
                },
                new Client
                {
                    ClientId = "gatewaytodownstreamtokenexchangeclient",
                    ClientName = "Gateway to Downstream Token Exchange Client",
                    AllowedGrantTypes = new[] { "urn:ietf:params:oauth:grant-type:token-exchange" },
                    RequireConsent = false,
                    ClientSecrets = { new Secret("0cdea0bc-779e-4368-b46b-09956f70712c".Sha256()) },
                    AllowedScopes = {
                         "openid", "profile", "eventcatalog.read", "eventcatalog.write", "shoppingbasket.fullaccess" }
                }
                
            };
    */

    public static IEnumerable<Client> GetClients(IConfiguration config)
    {
        var baseUrl = config["ClientUrls:BaseUrl"] ?? throw new InvalidOperationException("ClientUrls:BaseUrl is not configured.");

        return new[]
        {            
            new Client
            {
                ClientName = "GloboTicket Machine 2 Machine Client",
                ClientId = "globoticketm2m",
                ClientSecrets = { new Secret(config["Clients:M2M:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "eventcatalog.read", "discount.fullaccess" }
            },
            
            new Client
            {
                ClientName = "GloboTicket Interactive Client",
                ClientId = "globoticketinteractive",
                ClientSecrets = { new Secret(config["Clients:Interactive:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RedirectUris = { $"{baseUrl}/signin-oidc" },
                PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "globoticketgateway.fullaccess", "shoppingbasket.fullaccess", "eventcatalog.read", "eventcatalog.write" }
            },
            
            new Client
            {
                ClientName = "GloboTicket Interactive Gateway Client",
                ClientId = "globoticketinteractivegateway",
                ClientSecrets = { new Secret(config["Clients:InteractiveGateway:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RedirectUris = { $"{baseUrl}/signin-oidc" },
                PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
                AllowedScopes = { "openid", "profile", "globoticketgateway.fullaccess" }
            },
            
            new Client
            {
                ClientName = "GloboTicket Client",
                ClientId = "globoticket",
                ClientSecrets = { new Secret(config["Clients:GloboTicket:Secret"].Sha256()) },
                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                RedirectUris = { $"{baseUrl}/signin-oidc" },
                PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
                AllowedScopes = { "openid", "profile", "shoppingbasket.fullaccess", "eventcatalog.read", "eventcatalog.write" }
            },
            
            new Client
            {                
                ClientName = "Shopping Basket Token Exchange Client",
                ClientId = "shoppingbaskettodownstreamtokenexchangeclient",
                AllowedGrantTypes = new[] { "urn:ietf:params:oauth:grant-type:token-exchange" },
                ClientSecrets = { new Secret(config["Clients:ShoppingBasketTokenExchange:Secret"].Sha256()) },
                AllowedScopes = { "openid", "profile", "discount.fullaccess", "ordering.fullaccess" }
            },
            
            new Client
            {                
                ClientName = "Gateway to Downstream Token Exchange Client",
                ClientId = "gatewaytodownstreamtokenexchangeclient",
                AllowedGrantTypes = new[] { "urn:ietf:params:oauth:grant-type:token-exchange" },
                RequireConsent = false,
                ClientSecrets = { new Secret(config["Clients:GatewayTokenExchange:Secret"].Sha256()) },
                AllowedScopes = { "openid", "profile", "eventcatalog.read", "eventcatalog.write", "shoppingbasket.fullaccess" }
            }
        };
    }

}