using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace GloboTicket.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        public async Task Logout()
        {
            // Clear the local cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Redirects to the IDP linked to scheme "OpenIdConnectDefaults.AuthenticationScheme" (oidc) so
            // it can clear its own session/cookie
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}