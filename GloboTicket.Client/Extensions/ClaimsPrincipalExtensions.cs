using System.Security.Claims;
using System;

namespace GloboTicket.Web.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // public static Guid GetUserId(this ClaimsPrincipal user)
        // {
        //     return Guid.Parse(user.FindFirstValue("sub"));
        // }

        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                // Fallback for development = single hardcoded user
                return Guid.Parse("E455A3DF-7FA5-47E0-8435-179B300D531F");
            }

            var sub = user.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(sub))
                throw new InvalidOperationException("No 'sub' claim present.");

            return Guid.Parse(sub);
        }
    }
}