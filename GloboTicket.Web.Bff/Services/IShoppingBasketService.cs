using GloboTicket.Web.Models.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Coupon;
using GloboTicket.Gateway.Shared.Basket;

namespace GloboTicket.Web.Services
{
    public interface IShoppingBasketService
    {
        Task<BasketLineDto> AddToBasket(Guid basketId, BasketLineForCreationDto basketLine);
        Task<IEnumerable<BasketLineDto>> GetLinesForBasket(Guid basketId);
        Task<BasketDto> GetBasket(Guid basketId);
        Task UpdateLine(Guid basketId, BasketLineForUpdateDto basketLineForUpdate);
        Task RemoveLine(Guid basketId, Guid lineId);
        Task ApplyCouponToBasket(Guid basketId, CouponForUpdateDto couponForUpdate);
        Task<BasketForCheckoutDto> Checkout(Guid basketId, BasketForCheckoutDto basketForCheckout);
    }
}
