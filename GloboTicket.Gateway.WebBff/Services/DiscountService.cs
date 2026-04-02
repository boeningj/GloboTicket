using System;
using System.Threading.Tasks;

using System.Net.Http;
using GloboTicket.Gateway.Shared.Coupon;
using GloboTicket.Gateway.WebBff.Extensions;
using GloboTicket.Gateway.WebBff.Url;

namespace GloboTicket.Gateway.WebBff.Services
{
    public class DiscountService : IDiscountService
    {
        private readonly HttpClient client;

        public DiscountService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<CouponDto> GetCouponByCode(string code)
        {
            if (code == string.Empty)
                return null;

            var response = await client.GetAsync(DiscountOperations.GetCouponByCode(code));
            return await response.ReadContentAs<CouponDto>();
        }

        public async Task<CouponDto> GetCouponById(Guid couponId)
        {
            var response = await client.GetAsync(DiscountOperations.GetCouponById(couponId));
            return await response.ReadContentAs<CouponDto>();
        }

        public async Task UseCoupon(Guid couponId)
        {
            var response = await client.PutAsJson(DiscountOperations.UseCoupon(couponId), new CouponForUpdateDto());
        }
    }
}
