using System.Collections.Generic;
using System;

namespace GloboTicket.Gateway.WebBff.Url
{
    public class ShoppingBasketOperations
    {
        public static string CreateBasket() => "/api/baskets";
        public static string GetBasket(Guid basketId) => $"/api/baskets/{basketId}";
        public static string GetLines(Guid basketId) => $"/api/baskets/{basketId}/basketLines";
        public static string AddLine(Guid basketId) => $"/api/baskets/{basketId}/basketLines";
        public static string UpdateLine(Guid basketId, Guid lineId) => $"/api/baskets/{basketId}/basketLines/{lineId}";
        public static string DeleteLine(Guid basketId, Guid lineId) => $"/api/baskets/{basketId}/basketLines/{lineId}";
        public static string ApplyCoupon(Guid basketId) => $"/api/baskets/{basketId}/coupon";
        public static string Checkout() => $"/api/baskets/checkout";
    }
}