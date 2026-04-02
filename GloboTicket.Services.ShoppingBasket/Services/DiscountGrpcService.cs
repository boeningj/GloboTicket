using GloboTicket.Grpc;
using GloboTicket.Services.ShoppingBasket.Models;
using System;
using System.Threading.Tasks;
using Coupon = GloboTicket.Services.ShoppingBasket.Models.Coupon;

using Grpc.Core;
using Polly;

namespace GloboTicket.Services.ShoppingBasket.Services
{
    public class DiscountGrpcService : IDiscountService
    {
        private readonly Discounts.DiscountsClient discountsClient;
        private readonly IAsyncPolicy policy;

        public DiscountGrpcService(Discounts.DiscountsClient discountsClient)
        {
            this.discountsClient = discountsClient;
        }

        public DiscountGrpcService(
            Discounts.DiscountsClient discountsClient,
            IAsyncPolicy retryPolicy,
            IAsyncPolicy circuitBreakerPolicy)
        {
            this.discountsClient = discountsClient;
            this.policy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
        }

        public async Task<Coupon> GetCoupon(Guid userId)
        {
            return await policy.ExecuteAsync(async () =>
            {
                var response = await discountsClient.GetCouponByUserIdAsync(
                    new GetCouponByUserIdRequest  { UserId = userId.ToString() });

                return new Coupon
                {
                    CouponId = response.Coupon.CouponId != null
                        ? Guid.Parse(response.Coupon.CouponId)
                        : Guid.Empty,
                    Amount = response.Coupon.Amount
                };
            });
        }        

        public Task<Coupon> GetCouponWithError(Guid userId)
        {
            // Optional: implement a gRPC error test
            throw new NotImplementedException();
        }
    }
}