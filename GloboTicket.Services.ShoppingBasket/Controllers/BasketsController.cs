using AutoMapper;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.ShoppingBasket.Messages;
using GloboTicket.Services.ShoppingBasket.Models;
using GloboTicket.Services.ShoppingBasket.Repositories;
using GloboTicket.Services.ShoppingBasket.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Polly.CircuitBreaker;
using GloboTicket.Grpc;
using Grpc.Net.Client;
using Coupon = GloboTicket.Services.ShoppingBasket.Models.Coupon;
using Microsoft.Extensions.Logging;

using IdentityModel;
using System.Security.Claims;
using GloboTicket.Services.ShoppingBasket.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;

namespace GloboTicket.Services.ShoppingBasket.Controllers
{
    [Route("api/baskets")]
    [ApiController]
    public class BasketsController : ControllerBase
    {
        private readonly IBasketRepository basketRepository;
        private readonly IMapper mapper;
        private readonly IMessageBus messageBus;
        private readonly IDiscountService discountService;
        private readonly ILogger<BasketsController> _logger;
        private readonly string authMode;
        // TODO? - You can get rid of the null conditional operator by having this outside your authMode switch in Startup.cs:
        // services.AddHttpContextAccessor();
        // services.AddScoped<TokenExchangeService>();
        // And also adding:  service.AddAccessTokenManagement(); which is necessary for the IClientAccessTokenCache that caches the exchanged token
        private readonly TokenExchangeService? tokenExchangeService;
        private readonly string orderingScope;

        public BasketsController(IBasketRepository basketRepository, IMapper mapper, IMessageBus messageBus, IDiscountService discountService,
            ILogger<BasketsController> logger, IConfiguration configuration, TokenExchangeService? tokenExchangeService = null)
        {
            this.basketRepository = basketRepository;
            this.mapper = mapper;
            this.messageBus = messageBus;
            this.discountService = discountService;
            this._logger = logger;
            this.authMode = configuration.GetValue<string>("AuthenticationOptions:AuthMode");
            this.tokenExchangeService = tokenExchangeService;
            this.orderingScope = configuration["Identity:TokenExchange:Scopes:Ordering"] ?? throw new InvalidOperationException("Ordering scope is not configured.");
        }

        [HttpGet("{basketId}", Name = "GetBasket")]
        public async Task<ActionResult<Basket>> Get(Guid basketId)
        {
            var basket = await basketRepository.GetBasketById(basketId);
            if (basket == null)
            {
                return NotFound();
            }

            var result = mapper.Map<Basket>(basket);
            result.NumberOfItems = basket.BasketLines.Sum(bl => bl.TicketAmount);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Basket>> Post(BasketForCreation basketForCreation)
        {
            var basketEntity = mapper.Map<Entities.Basket>(basketForCreation);

            basketRepository.AddBasket(basketEntity);
            await basketRepository.SaveChanges();

            var basketToReturn = mapper.Map<Basket>(basketEntity);

            return CreatedAtRoute(
                "GetBasket",
                new { basketId = basketEntity.BasketId },
                basketToReturn);
        }

        [HttpPut("{basketId}/coupon")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ApplyCouponToBasket(Guid basketId, Coupon coupon)
        {
            var basket = await basketRepository.GetBasketById(basketId);

            if (basket == null)
            {
                return BadRequest();
            }

            basket.CouponId = coupon.CouponId;
            await basketRepository.SaveChanges();

            return Accepted();
        }

        [HttpPost("checkout")]
        [ProducesResponseType((int)HttpStatusCode.Accepted)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> CheckoutBasketAsync([FromBody] BasketCheckout basketCheckout)
        {
            try
            {
                //based on basket checkout, fetch the basket lines from repo
                var basket = await basketRepository.GetBasketById(basketCheckout.BasketId);

                if (basket == null)
                {
                    return BadRequest();
                }

                BasketCheckoutMessage basketCheckoutMessage = mapper.Map<BasketCheckoutMessage>(basketCheckout);
                basketCheckoutMessage.Id = Guid.NewGuid();
                basketCheckoutMessage.BasketLines = new List<BasketLineMessage>();                
                int total = 0;

                foreach (var b in basket.BasketLines)
                {
                    var basketLineMessage = new BasketLineMessage
                    {
                        BasketId = b.BasketId,
                        BasketLineId = b.BasketLineId,
                        Price = b.Price,
                        TicketAmount = b.TicketAmount,
                        EventId = b.Event.EventId,
                        EventName = b.Event.Name,
                        EventDate = b.Event.Date,
                        VenueName = b.Event.VenueName,
                        VenueCity = b.Event.VenueCity,
                        VenueCountry = b.Event.VenueCountry
                    };

                    total += b.Price * b.TicketAmount;

                    basketCheckoutMessage.BasketLines.Add(basketLineMessage);
                }

                //apply discount by talking to the discount service
                Coupon coupon = null;

                //No longer needed as Startup.cs & DI will handle which type of client to use (Http or gRPC) based on config flag
                // var channel = GrpcChannel.ForAddress("https://localhost:5007");//Configuration["ApiConfigs:Discount:Uri"]);
                // DiscountService discountService = new DiscountService(new Discounts.DiscountsClient(channel));

                // if (basket.CouponId.HasValue)
                //     coupon = await discountService.GetCoupon(basket.CouponId.Value);

                // Used for testing policies:
                // if (basket.CouponId.HasValue)
                //     coupon = await discountService.GetCouponWithError(basket.CouponId.Value);
                
                // var sub = User.FindFirstValue(JwtClaimTypes.Subject);
                // _logger.LogInformation("CheckoutBasket called. sub claim = {Sub}", sub);
                // _logger.LogInformation("CheckoutBasket payload UserId = {UserId}", basketCheckout.UserId);

                // // IRL, get the user id from this.User object
                // var userId = basketCheckout.UserId;

                // if (!(userId == Guid.Empty))
                //     coupon = await discountService.GetCoupon(userId);

                Guid userId;

                // Try to get user ID from JWT (AccessTokens or TrustGateway modes)
                var sub = User.FindFirstValue(JwtClaimTypes.Subject);
                if (!string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out var parsedSub))
                {
                    userId = parsedSub;
                    _logger.LogInformation("CheckoutBasket called. Using 'sub' from JWT: {UserId}", userId);
                }
                else if (basketCheckout.UserId != Guid.Empty)
                {
                    // Fallback for None mode (no auth), or when JWT not present
                    userId = basketCheckout.UserId;
                    _logger.LogInformation("CheckoutBasket called. Using UserId from request body: {UserId}", userId);
                }
                else
                {
                    _logger.LogWarning("CheckoutBasket called without a valid user ID.");
                    return Unauthorized();
                }

                coupon = await discountService.GetCoupon(userId);
                
                if (coupon != null)
                {
                    basketCheckoutMessage.BasketTotal = total - coupon.Amount;
                }
                else
                {
                    basketCheckoutMessage.BasketTotal = total;
                }
                
                if (authMode != "None")
                {
                    // Get the incoming access token
                    var incomingToken = await HttpContext.GetTokenAsync("access_token");
                    
                    // Exchange the incoming access token passing along the cancellation token from the current HTTP request 
                    var accessTokenForOrderingService = await tokenExchangeService?.GetTokenAsync(incomingToken, orderingScope, HttpContext.RequestAborted);
                    //_logger.LogInformation("Exchanged token: {token}", accessTokenForOrderingService);

                    // Send the exchanged token in the message to the service bus so the ordering service can validate it downstream
                    basketCheckoutMessage.SecurityContext.AccessToken = accessTokenForOrderingService;
                }

                try
                {
                    await messageBus.PublishMessage(basketCheckoutMessage, "checkoutmessage");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                await basketRepository.ClearBasket(basketCheckout.BasketId);
                return Accepted(basketCheckoutMessage);
            }
            catch(BrokenCircuitException ex)
            {
                string message = ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError, ex.StackTrace);

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during checkout for BasketId {BasketId}", basketCheckout.BasketId);
                return StatusCode(StatusCodes.Status500InternalServerError, e.StackTrace);
            }
        }
    }
}
