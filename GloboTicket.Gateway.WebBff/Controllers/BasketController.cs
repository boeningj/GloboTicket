using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using GloboTicket.Gateway.Shared.Basket;   // <-- your shared DTOs live here
using GloboTicket.Gateway.Shared.Coupon;
using GloboTicket.Gateway.WebBff.Services;

namespace GloboTicket.Gateway.WebBff.Controllers
{
    [ApiController]
    [Route("api/bffweb/baskets")]
    public class BasketController : ControllerBase
    {
        private readonly IBasketService basketService;

        public BasketController(IBasketService basketService)
        {
            this.basketService = basketService;
        }

        //Get api/bffweb/baskets/{basketId}
        [HttpGet("{basketId}", Name = "GetBasket")]
        public async Task<ActionResult<BasketDto>> GetBasket(Guid basketId)
        {
            var basket = await basketService.GetBasket(basketId);
            if (basket == null)
                return NotFound();

            return Ok(basket);
        }

        //Get api/bffweb/baskets/{basketId}/basketlines
        [HttpGet("{basketId}/basketlines")]
        public async Task<ActionResult<IEnumerable<BasketLineDto>>> GetLinesForBasket(Guid basketId)
        {
            var lines = await basketService.GetLinesForBasket(basketId);
            return Ok(lines);
        }

        // POST api/bffweb/baskets/{basketId}/basketlines
        [HttpPost("{basketId}/basketlines")]
        public async Task<ActionResult<BasketLineDto>> AddLine(Guid basketId, BasketLineForCreationDto basketLine)
        {
            var line = await basketService.AddToBasket(basketId, basketLine);
            return CreatedAtRoute("GetBasket", new { basketId = line.BasketId }, line);
        }

        // PUT: api/bffweb/baskets/{basketId}/basketlines/{lineId}
        [HttpPut("{basketId}/basketlines/{lineId}")]
        public async Task<IActionResult> UpdateLine(Guid basketId, Guid lineId, BasketLineForUpdateDto basketLineForUpdate)
        {
            if (lineId != basketLineForUpdate.LineId)
                return BadRequest("LineId mismatch.");

            await basketService.UpdateLine(basketId, basketLineForUpdate);
            return NoContent();
        }

        // DELETE: api/bffweb/baskets/{basketId}/basketlines/{lineId}
        [HttpDelete("{basketId}/basketlines/{lineId}")]
        public async Task<IActionResult> RemoveLine(Guid basketId, Guid lineId)
        {
            await basketService.RemoveLine(basketId, lineId);
            return NoContent();
        }

        // PUT: api/bffweb/baskets/{basketId}/coupon
        [HttpPut("{basketId}/coupon")]
        public async Task<IActionResult> ApplyCouponToBasket(Guid basketId, CouponForUpdateDto couponForUpdate)
        {
            await basketService.ApplyCouponToBasket(basketId, couponForUpdate);
            return NoContent();
        }

        // POST: api/bffweb/baskets/{basketId}/checkout
        [HttpPost("{basketId}/checkout")]
        public async Task<ActionResult<BasketForCheckoutDto>> Checkout(Guid basketId, BasketForCheckoutDto basketForCheckout)
        {
            var result = await basketService.Checkout(basketId, basketForCheckout);
            return Ok(result);
        }

    }
}
