using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using GloboTicket.Gateway.WebBff.Services;
using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Gateway.WebBff.Controllers
{
    [ApiController]
    [Route("api/bffweb/order")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService orderService;

        public OrderController(IOrderService orderService)
        {
            this.orderService = orderService;
        }

        // GET: api/bffweb/order/user/{userId}
        [HttpGet("user/{userId:guid}")]
        public async Task<ActionResult<List<OrderDto>>> GetOrdersForUser(Guid userId)
        {
            var orders = await orderService.GetOrdersForUser(userId);
            if (orders == null || orders.Count == 0)
                return NotFound();

            return Ok(orders);
        }

        // GET: api/bffweb/order/{orderId}
        [HttpGet("{orderId:guid}")]
        public async Task<ActionResult<OrderDto>> GetOrderDetails(Guid orderId)
        {
            var order = await orderService.GetOrderDetails(orderId);
            if (order == null)
                return NotFound();

            return Ok(order);
        }

    }
}
