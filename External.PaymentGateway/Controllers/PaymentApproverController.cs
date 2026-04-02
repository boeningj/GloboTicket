using External.PaymentGateway.Model;
using Microsoft.AspNetCore.Mvc;
using System;

namespace External.PaymentGateway.Controllers
{
    [ApiController]
    [Route("api/paymentapprover")]
    public class PaymentApproverController : Controller
    {
        private static readonly Random _random = new Random();

        [HttpPost]
        public IActionResult TryPayment([FromBody] PaymentDto payment)
        {

            // int num = new Random().Next(1000);
            // if (num > 500)
            //     return Ok(true);

            // return Ok(false);

            int num = _random.Next(100); // 0..99
            if (num < 80)                // 0..79 → 80% chance
                return Ok(true);

            return Ok(false);            // 20% chance
        }
    }
}
