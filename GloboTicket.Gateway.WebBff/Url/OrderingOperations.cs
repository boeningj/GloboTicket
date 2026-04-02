using System;

namespace GloboTicket.Gateway.WebBff.Url
{
    public class OrderingOperations
    {
        public static string GetOrdersForUser(Guid userId) => $"/api/order/user/{userId}";
        public static string GetOrderDetails(Guid orderId) => $"/api/order/{orderId}";
    }
}