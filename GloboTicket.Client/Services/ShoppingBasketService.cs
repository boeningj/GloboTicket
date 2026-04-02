using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models;
using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace GloboTicket.Web.Services
{
    // -----------------------------------------------------------------------------
    // IMPORTANT: HttpClient BaseAddress + relative URI behavior
    //
    // Key rule:
    //   Relative path  → appended to BaseAddress
    //   Absolute path  → replaces BaseAddress path
    //
    // Example:
    //   BaseAddress = https://host/prefix/
    //
    //   GetAsync("api/x") → https://host/prefix/api/x        correct
    //   GetAsync("/api/x") → https://host/api/x              prefix LOST
    //
    // WHY THIS MATTERS:
    // Our services use BaseAddress prefixes like:  https://localhost:5051/order/
    //
    // Using a leading '/' will bypass the gateway prefix and break routing.
    //
    // ✅ ALWAYS use relative paths without leading '/':
    //     "api/order/..."
    // ❌ NEVER use:
    //     "/api/order/..."
    //
    // This bug already occurred once — do not remove this rule.
    // -----------------------------------------------------------------------------
    public class ShoppingBasketService : IShoppingBasketService
    {
        private readonly HttpClient client;
        //private readonly Settings settings;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger<ShoppingBasketService> logger;        

        public ShoppingBasketService(HttpClient client, IHttpContextAccessor httpContextAccessor, ILogger<ShoppingBasketService> logger)
        {
            this.client = client;
            this.httpContextAccessor = httpContextAccessor;
            this.logger = logger;
        }

        public async Task<BasketLine> AddToBasket(Guid basketId, BasketLineForCreation basketLine)
        {
            if (basketId == Guid.Empty)
            {
                basketId = Guid.NewGuid();
            }

            // Check if the basket exists in the API
            var basketResponse = await client.GetAsync($"api/baskets/{basketId}");
            
            if (basketResponse.StatusCode == HttpStatusCode.NotFound)
            {
                //logger.LogInformation("Sending BasketForCreation.UserId = {UserId}", settings.UserId);

                var user = httpContextAccessor.HttpContext?.User 
                    ?? throw new InvalidOperationException("No authenticated user.");

                // Basket doesn't exist → create it via BasketsController
                var createBasketResponse = await client.PostAsJson(
                    "api/baskets",
                    //new BasketForCreation { UserId = settings.UserId } // still hardcoded in Setting.cs for now
                    new BasketForCreation
                    {
                        UserId = user.GetUserId()
                    }
                );

                createBasketResponse.EnsureSuccessStatusCode();

                // Deserialize the newly created basket
                var basket = await createBasketResponse.ReadContentAs<Basket>();

                // Update the basketId to match what was just created
                basketId = basket.BasketId;
            }
            else
            {
                // Any other unexpected error
                basketResponse.EnsureSuccessStatusCode();
            }

            // Add the basket line
            var response = await client.PostAsJson($"api/baskets/{basketId}/basketlines", basketLine);
            response.EnsureSuccessStatusCode();
            
            return await response.ReadContentAs<BasketLine>();
        }

        public async Task<Basket> GetBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return null;

            var response = await client.GetAsync($"api/baskets/{basketId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Basket not found → return empty basket
                return new Basket
                {
                    BasketId = basketId,
                    NumberOfItems = 0
                };
            }

            /*
            HttpClient.GetAsync() above returns an HttpResponseMessage.
            The StatusCode could be anything: 200 OK, 404 Not Found, 500 InternalServerError, etc.
            EnsureSuccessStatusCode() throws an exception if the status code is not in the 2xx range.
            It guarantees that if the server returned an error other than 404, your code won’t silently continue — instead, it will throw an exception.
            Since you already explicitly handle a 404 error, it means EnsureSuccessStatusCode() will only throw if you get other unexpected HTTP errors,
            e.g., 500, 403, 401.  This is useful because you don’t want to call ReadContentAs<Basket>() on a failed response, which could throw or give bad data.
            If you want, you could remove the line and just call ReadContentAs<Basket>() unconditionally. But then:
            If the server returned 500, 403, etc., ReadContentAs may fail in a less predictable way.
            Using EnsureSuccessStatusCode() makes your intention clear: “Only read the content if it succeeded; otherwise throw.”
            In short: it’s a safety check to catch unexpected server errors, but not strictly needed if you’re okay with letting ReadContentAs handle it.

            Return gets hit if the API responds with 2xx.
            Return is skipped if the API responds with 404 (handled earlier) or other non-success status (exception thrown).
            This is why EnsureSuccessStatusCode() is useful — it catches unexpected errors without letting invalid content get deserialized.
            */
            response.EnsureSuccessStatusCode();
            return await response.ReadContentAs<Basket>();
        }

        public async Task<IEnumerable<BasketLine>> GetLinesForBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return new BasketLine[0];
            var response = await client.GetAsync($"api/baskets/{basketId}/basketLines");
            return await response.ReadContentAs<BasketLine[]>();

        }

        public async Task UpdateLine(Guid basketId, BasketLineForUpdate basketLineForUpdate)
        {
            await client.PutAsJson($"api/baskets/{basketId}/basketLines/{basketLineForUpdate.LineId}", basketLineForUpdate);
        }

        public async Task RemoveLine(Guid basketId, Guid lineId)
        {
            await client.DeleteAsync($"api/baskets/{basketId}/basketLines/{lineId}");
        }

        public async Task ApplyCouponToBasket(Guid basketId, CouponForUpdate couponForUpdate)
        {
            var response = await client.PutAsJson($"api/baskets/{basketId}/coupon", couponForUpdate);
            //return await response.ReadContentAs<Coupon>();
        }

        public async Task<BasketForCheckout> Checkout(Guid basketId, BasketForCheckout basketForCheckout)
        {
            var response = await client.PostAsJson($"api/baskets/checkout", basketForCheckout);
            if (response.IsSuccessStatusCode)
                return await response.ReadContentAs<BasketForCheckout>();
            else
            {
                throw new Exception("Something went wrong placing your order. Please try again.");
            }
        }
    }
}
