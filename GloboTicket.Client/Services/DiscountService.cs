using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models.Api;
using System;
using System.Net.Http;
using System.Threading.Tasks;

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
    public class DiscountService : IDiscountService
    {
        private readonly HttpClient client;

        public DiscountService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<Coupon> GetCouponByCode(string code)
        {
            if (code == string.Empty)
                return null;

            var response = await client.GetAsync($"api/discount/code/{code}");
            return await response.ReadContentAs<Coupon>();
        }

        public async Task<Coupon> GetCouponById(Guid couponId)
        {
            var response = await client.GetAsync($"api/discount/{couponId}");
            return await response.ReadContentAs<Coupon>();
        }

        public async Task UseCoupon(Guid couponId)
        {
            var response = await client.PutAsJson($"api/discount/use/{couponId}", new CouponForUpdate());
        }
    }
}
