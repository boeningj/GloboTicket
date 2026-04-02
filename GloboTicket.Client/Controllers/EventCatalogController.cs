using System;
using System.Threading.Tasks;
using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models;
using GloboTicket.Web.Models.Api;
using GloboTicket.Web.Models.View;
using GloboTicket.Web.Services;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace GloboTicket.Web.Controllers
{
    public class EventCatalogController : Controller
    {
        private readonly IEventCatalogService eventCatalogService;
        private readonly IShoppingBasketService shoppingBasketService;
        private readonly Settings settings;
        private readonly ILogger<EventCatalogController> _logger;

        public EventCatalogController(IEventCatalogService eventCatalogService, IShoppingBasketService shoppingBasketService, Settings settings, ILogger<EventCatalogController> logger)
        {
            this.eventCatalogService = eventCatalogService;
            this.shoppingBasketService = shoppingBasketService;
            this.settings = settings;
            this._logger = logger;
        }

        public async Task<IActionResult> Index(Guid categoryId)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                await LogIdentityInformation();
            }
            // var currentBasketId = Request.Cookies.GetCurrentBasketId(settings);

            // var getBasket = currentBasketId == Guid.Empty ? Task.FromResult<Basket>(null) :
            //     shoppingBasketService.GetBasket(currentBasketId);

            // Get basket ID from cookie or create new
            var currentBasketId = Request.Cookies.GetCurrentBasketId(settings);
            if (currentBasketId == Guid.Empty)
            {
                currentBasketId = Guid.NewGuid();
                Response.Cookies.Append(
                    settings.BasketIdCookieName,
                    currentBasketId.ToString(),
                    new CookieOptions { HttpOnly = true, IsEssential = true }
                );
            }

            // Fetch basket, categories, events concurrently
            var getBasket = shoppingBasketService.GetBasket(currentBasketId);

            var getCategories = eventCatalogService.GetCategories();

            var getEvents = categoryId == Guid.Empty ? eventCatalogService.GetAll() :
                eventCatalogService.GetByCategoryId(categoryId);

            await Task.WhenAll(new Task[] { getBasket, getCategories, getEvents });

            var numberOfItems = getBasket.Result?.NumberOfItems ?? 0;

            return View(
                new EventListModel
                {
                    Events = getEvents.Result,
                    Categories = getCategories.Result,
                    NumberOfItems = numberOfItems,
                    SelectedCategory = categoryId
                }
            );
        }

        [HttpPost]
        public IActionResult SelectCategory([FromForm] Guid selectedCategory)
        {
            return RedirectToAction("Index", new { categoryId = selectedCategory });
        }

        public async Task<IActionResult> Detail(Guid eventId)
        {
            var ev = await eventCatalogService.GetEvent(eventId);
            return View(ev);
        }

        public async Task LogIdentityInformation()
        {
            // Get the saved identity token
            var identityToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken);

            // Get the saved access token
            var accessToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);

            // Get the refresh token
            var refreshToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);

            var userClaimsStringBuilder = new StringBuilder();
            foreach (var claim in User.Claims)
            {
                userClaimsStringBuilder.AppendLine($"Claim type: {claim.Type} - Claim value: {claim.Value}");
            }

            // Log token & claims
            _logger.LogInformation($"Identity token & user claims: " + $"\n{identityToken} \n{userClaimsStringBuilder}");
            _logger.LogInformation($"Access token: " + $"\n{accessToken}");
            _logger.LogInformation($"Refresh token: " + $"\n{refreshToken}");
        }
    }
}
