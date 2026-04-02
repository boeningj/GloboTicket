using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using System;
using System.Net.Http;
using System.Threading.Tasks;

using GloboTicket.Gateway.Shared.Coupon;

namespace GloboTicket.Web.Services
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

            var response = await client.GetAsync($"/api/bffweb/discount/code/{code}");
            return await response.ReadContentAs<CouponDto>();
        }

        public async Task<CouponDto> GetCouponById(Guid couponId)
        {
            var response = await client.GetAsync($"/api/bffweb/discount/{couponId}");
            return await response.ReadContentAs<CouponDto>();
        }

        public async Task UseCoupon(Guid couponId)
        {
            var response = await client.PutAsJson($"/api/bffweb/discount/use/{couponId}", new CouponForUpdateDto());
        }
    }
}
