using System;

namespace GloboTicket.Gateway.WebBff.Url
{
    public class DiscountOperations
    {
        public static string GetCouponByCode(string code) => $"/api/discount/code/{code}";
        public static string GetCouponById(Guid couponId) => $"/api/discount/{couponId}";
        public static string UseCoupon(Guid couponId) => $"/api/discount/use/{couponId}";
    }
}