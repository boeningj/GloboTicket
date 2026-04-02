using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using GloboTicket.Gateway.WebBff.Services;
using GloboTicket.Gateway.Shared.Coupon;

namespace GloboTicket.Gateway.WebBff.Controllers
{
    [ApiController]
    [Route("api/bffweb/discount")]
    public class DiscountController : Controller
    {
        private readonly IDiscountService discountService;

        public DiscountController(IDiscountService discountService)
        {
            this.discountService = discountService;
        }

        // GET api/bffweb/discount/code/{code}
        [HttpGet("code/{code}")]
        public async Task<ActionResult<CouponDto>> GetCouponByCode(string code)
        {
            var coupon = await discountService.GetCouponByCode(code);
            if (coupon == null)
                return NotFound();

            return Ok(coupon);
        }

        // GET: api/bffweb/discount/{couponId}
        [HttpGet("{couponId:guid}")]
        public async Task<ActionResult<CouponDto>> GetCouponById(Guid couponId)
        {
            var coupon = await discountService.GetCouponById(couponId);
            if (coupon == null)
                return NotFound();

            return Ok(coupon);
        }

        // PUT: api/bffweb/discount/use/{couponId}
        [HttpPut("use/{couponId:guid}")]
        public async Task<IActionResult> UseCoupon(Guid couponId)
        {
            await discountService.UseCoupon(couponId);
            return NoContent();
        }
        
    }
}
