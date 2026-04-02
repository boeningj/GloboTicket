using GloboTicket.Gateway.Shared.Basket;
using GloboTicket.Gateway.WebBff.Extensions;
using System;
using System.Net.Http;
using System.Threading.Tasks;

using System.Collections.Generic;
using GloboTicket.Gateway.WebBff.Url;
using GloboTicket.Gateway.Shared;
using GloboTicket.Gateway.Shared.Coupon;
using System.Net;

namespace GloboTicket.Gateway.WebBff.Services
{
    public class BasketService : IBasketService
    {
        private readonly HttpClient client;

        private readonly Settings settings;

        public BasketService(HttpClient client, Settings settings)
        {
            this.client = client;
            this.settings = settings;
        }

        public async Task<BasketLineDto> AddToBasket(Guid basketId, BasketLineForCreationDto basketLine)
        {
            // if (basketId == Guid.Empty)
            // {
            //     var basketResponse = await client.PostAsJson(ShoppingBasketOperations.CreateBasket(), new BasketForCreationDto { UserId = settings.UserId });
            //     var basket = await basketResponse.ReadContentAs<BasketDto>();
            //     basketId = basket.BasketId;
            // }

            // var response = await client.PostAsJson(ShoppingBasketOperations.AddLine(basketId), basketLine);
            // return await response.ReadContentAs<BasketLineDto>();

            if (basketId == Guid.Empty)
            {
                basketId = Guid.NewGuid();
            }

            //Check if the basket exists in the API
            var basketResponse = await client.GetAsync(ShoppingBasketOperations.GetBasket(basketId));

            if (basketResponse.StatusCode == HttpStatusCode.NotFound)
            {
                //Basket doesn't exist -> create it via BasketsController
                var createBasketResponse = await client.PostAsJson(ShoppingBasketOperations.CreateBasket(),
                    new BasketForCreationDto { UserId = settings.UserId } // still hardcoded in Setting.cs for now
                );

                createBasketResponse.EnsureSuccessStatusCode();

                //Deserialize the newly created basket
                var basket = await createBasketResponse.ReadContentAs<BasketDto>();

                //Update the basketId to match what was just created
                basketId = basket.BasketId;
            }
            else
            {
                //Any other unexpected error
                basketResponse.EnsureSuccessStatusCode();
            }

            //Add the basket line
            var response = await client.PostAsJson(ShoppingBasketOperations.AddLine(basketId), basketLine);
            response.EnsureSuccessStatusCode();

            return await response.ReadContentAs<BasketLineDto>();
        }

        public async Task<BasketDto> GetBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return null;

            var response = await client.GetAsync(ShoppingBasketOperations.GetBasket(basketId));

            // If the basket doesn't exist, return an empty basket instead of throwing
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new BasketDto
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
            return await response.ReadContentAs<BasketDto>();
        }

        public async Task<IEnumerable<BasketLineDto>> GetLinesForBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return new BasketLineDto[0];            
            var response = await client.GetAsync(ShoppingBasketOperations.GetLines(basketId));
            return await response.ReadContentAs<BasketLineDto[]>();
        }

        public async Task UpdateLine(Guid basketId, BasketLineForUpdateDto basketLineForUpdate)
        {
            await client.PutAsJson(ShoppingBasketOperations.UpdateLine(basketId, basketLineForUpdate.LineId), basketLineForUpdate);
        }

        public async Task RemoveLine(Guid basketId, Guid lineId)
        {
            await client.DeleteAsync(ShoppingBasketOperations.DeleteLine(basketId, lineId));
        }

        public async Task ApplyCouponToBasket(Guid basketId, CouponForUpdateDto couponForUpdate)
        {
            var response = await client.PutAsJson(ShoppingBasketOperations.ApplyCoupon(basketId), couponForUpdate);
            //return await response.ReadContentAs<Coupon>();
        }

        public async Task<BasketForCheckoutDto> Checkout(Guid basketId, BasketForCheckoutDto basketForCheckout)
        {
            var response = await client.PostAsJson(ShoppingBasketOperations.Checkout(), basketForCheckout);
            if(response.IsSuccessStatusCode)
                return await response.ReadContentAs<BasketForCheckoutDto>();
            else
            {
                throw new Exception("Something went wrong placing your order. Please try again.");
            }
        }
    }
}