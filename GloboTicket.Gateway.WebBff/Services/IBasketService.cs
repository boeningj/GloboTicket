using GloboTicket.Gateway.Shared.Basket;
using GloboTicket.Gateway.Shared.Coupon;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GloboTicket.Gateway.WebBff.Services
{
    public interface IBasketService
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