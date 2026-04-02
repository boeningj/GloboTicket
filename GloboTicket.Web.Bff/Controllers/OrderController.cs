using GloboTicket.Web.Models;
using GloboTicket.Web.Models.View;
using GloboTicket.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

using System;
using System.Linq;
using System.Collections.Generic;
using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Web.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService orderService;
        private readonly Settings settings;

        public OrderController(Settings settings, IOrderService orderService)
        {
            this.settings = settings;
            this.orderService = orderService;
        }

        public async Task<IActionResult> Index()
        {
            // var orders = await orderService.GetOrdersForUser(settings.UserId);

            // return View(new OrderViewModel { Orders = orders });
            List<OrderDto> orders = await orderService.GetOrdersForUser(settings.UserId);
            var numberOfMessages = (from order in orders
                                    where order.Message != null
                                    select order).Count();

            bool hasMessages = numberOfMessages > 0;

            var vm = new OrderListViewModel
            {
                HasMessage = hasMessages,
                Orders = orders
            };

            return View(vm);
        }

        public async Task<IActionResult> Detail(Guid orderId)
        {
            var ev = await orderService.GetOrderDetails(orderId);
            return View(ev);
        }
    }
}
