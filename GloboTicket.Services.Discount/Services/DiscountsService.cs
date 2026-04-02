using AutoMapper;
using GloboTicket.Grpc;
using GloboTicket.Services.Discount.Repositories;
using Grpc.Core;
using System;
using System.Threading.Tasks;

namespace GloboTicket.Services.Discount.Services
{
    public class DiscountsService: Discounts.DiscountsBase
    {
        private readonly ICouponRepository couponRepository;
        private readonly IMapper mapper;

        public DiscountsService(IMapper mapper, ICouponRepository couponRepository)
        {
            this.mapper = mapper;
            this.couponRepository = couponRepository;
        }

        public override async Task<GetCouponByUserIdResponse> GetCouponByUserId(GetCouponByUserIdRequest request, ServerCallContext context)
        {
            var response = new GetCouponByUserIdResponse();
            var coupon = await couponRepository.GetCouponByUserId(Guid.Parse(request.UserId));
            response.Coupon = new Coupon
            {
                CouponId = coupon.CouponId.ToString(),
                Amount = coupon.Amount,                
            };
            return response;
        }
    }
}