using System;
using System.ComponentModel.DataAnnotations;

namespace GloboTicket.Gateway.Shared.Coupon
{
    public class CouponForUpdateDto
    {
        [Required]
        public Guid CouponId { get; set; }
    }
}