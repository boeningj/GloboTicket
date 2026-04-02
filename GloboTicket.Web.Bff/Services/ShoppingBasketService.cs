using GloboTicket.Gateway.Shared.Basket;
using GloboTicket.Gateway.Shared.Coupon;
using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models;
using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace GloboTicket.Web.Services
{
    public class ShoppingBasketService : IShoppingBasketService
    {
        private readonly HttpClient client;
        private readonly Settings settings;

        public ShoppingBasketService(HttpClient client, Settings settings)
        {
            this.client = client;
            this.settings = settings;
        }

        public async Task<BasketLineDto> AddToBasket(Guid basketId, BasketLineForCreationDto basketLine)
        {
            if (basketId == Guid.Empty)
            {
                var basketResponse = await client.PostAsJson("api/bffweb/baskets", new BasketForCreationDto { UserId = settings.UserId });
                var basket = await basketResponse.ReadContentAs<BasketDto>();
                basketId = basket.BasketId;
            }

            var response = await client.PostAsJson($"api/bffweb/baskets/{basketId}/basketlines", basketLine);
            return await response.ReadContentAs<BasketLineDto>();
        }

        public async Task<BasketDto> GetBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return null;
            var response = await client.GetAsync($"api/bffweb/baskets/{basketId}");
            return await response.ReadContentAs<BasketDto>();
        }

        public async Task<IEnumerable<BasketLineDto>> GetLinesForBasket(Guid basketId)
        {
            if (basketId == Guid.Empty)
                return new BasketLineDto[0];
            var response = await client.GetAsync($"api/bffweb/baskets/{basketId}/basketlines");
            return await response.ReadContentAs<BasketLineDto[]>();

        }

        public async Task UpdateLine(Guid basketId, BasketLineForUpdateDto basketLineForUpdate)
        {
            await client.PutAsJson($"api/bffweb/baskets/{basketId}/basketlines/{basketLineForUpdate.LineId}", basketLineForUpdate);
        }

        public async Task RemoveLine(Guid basketId, Guid lineId)
        {
            await client.DeleteAsync($"api/bffweb/baskets/{basketId}/basketlines/{lineId}");
        }

        public async Task ApplyCouponToBasket(Guid basketId, CouponForUpdateDto couponForUpdate)
        {
            var response = await client.PutAsJson($"api/bffweb/baskets/{basketId}/coupon", couponForUpdate);
            //return await response.ReadContentAs<Coupon>();
        }

        public async Task<BasketForCheckoutDto> Checkout(Guid basketId, BasketForCheckoutDto basketForCheckout)
        {
            var response = await client.PostAsJson($"api/bffweb/baskets/{basketId}/checkout", basketForCheckout);
            if(response.IsSuccessStatusCode)
                return await response.ReadContentAs<BasketForCheckoutDto>();
            else
            {
                throw new Exception("Something went wrong placing your order. Please try again.");
            }
        }
    }
}
