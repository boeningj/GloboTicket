using System;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Coupon;

namespace GloboTicket.Gateway.WebBff.Services
{
    public interface IDiscountService
    {        
        Task<CouponDto> GetCouponByCode(string code);
        Task UseCoupon(Guid couponId);
        Task<CouponDto> GetCouponById(Guid couponId);        
    }
}
