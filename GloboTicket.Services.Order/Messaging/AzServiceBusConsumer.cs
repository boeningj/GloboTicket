using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Ordering.Entities;
using GloboTicket.Services.Ordering.Messages;
using GloboTicket.Services.Ordering.Repositories;
//using Microsoft.Azure.ServiceBus;
//using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Azure.Messaging.ServiceBus;
using System.Collections.Generic;
using GloboTicket.Services.Ordering.Helpers;


namespace GloboTicket.Services.Ordering.Messaging
{   
    public class AzServiceBusConsumer : IAzServiceBusConsumer, IAsyncDisposable
    {
        private readonly string checkoutSubscriptionName = "globoticketorder-checkout";
        private readonly string paymentUpdateSubscriptionName = "globoticketorder-paymentupdate";

        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;

        private ServiceBusClient _client;
        private ServiceBusProcessor _checkoutProcessor;
        private ServiceBusProcessor _paymentUpdateProcessor;

        private readonly string checkoutMessageTopic;
        private readonly string orderPaymentRequestMessageTopic;
        private readonly string orderPaymentUpdatedMessageTopic;
        private readonly string _authMode;

        private bool _started = false;

        public AzServiceBusConsumer(IConfiguration configuration, IMessageBus messageBus, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _messageBus = messageBus;
            _serviceProvider = serviceProvider;

            _authMode = configuration.GetValue<string>("AuthenticationOptions:AuthMode");
            var serviceBusConnectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            checkoutMessageTopic = _configuration.GetValue<string>("CheckoutMessageTopic");
            //For publishing messages to the Payment service
            orderPaymentRequestMessageTopic = _configuration.GetValue<string>("OrderPaymentRequestMessageTopic");
            orderPaymentUpdatedMessageTopic = _configuration.GetValue<string>("OrderPaymentUpdatedMessageTopic");

            _client = new ServiceBusClient(serviceBusConnectionString);

            _checkoutProcessor = _client.CreateProcessor(checkoutMessageTopic, checkoutSubscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            });

            _paymentUpdateProcessor = _client.CreateProcessor(orderPaymentUpdatedMessageTopic, paymentUpdateSubscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            });
        }

        public async Task StartAsync()
        {
            if (_started) return;
            _started = true;

            Console.WriteLine("[ServiceBusConsumer] Starting message handlers...");

            _checkoutProcessor.ProcessMessageAsync += OnCheckoutMessageReceived;
            _checkoutProcessor.ProcessErrorAsync += OnErrorReceived;

            _paymentUpdateProcessor.ProcessMessageAsync += OnOrderPaymentUpdateReceived;
            _paymentUpdateProcessor.ProcessErrorAsync += OnErrorReceived;

            await _checkoutProcessor.StartProcessingAsync();
            await _paymentUpdateProcessor.StartProcessingAsync();

            Console.WriteLine("[ServiceBusConsumer] Handlers registered successfully.");
        }

        private async Task OnCheckoutMessageReceived(ProcessMessageEventArgs args)
        {
            var messageId = args.Message.MessageId;
            Console.WriteLine($"[Checkout] Received message: {args.Message.MessageId}");
            Console.WriteLine($"[Checkout] AuthMode: {_authMode}");

            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var customerRepository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var processedRepository = scope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();

            //Check if message was already processed
            if (await processedRepository.HasBeenProcessedAsync(messageId))
            {
                Console.WriteLine($"[Checkout] Skipping duplicate message {messageId}");
                await args.CompleteMessageAsync(args.Message);
                return;
            }
            
            var body = args.Message.Body.ToString();
            var basketCheckoutMessage = JsonConvert.DeserializeObject<BasketCheckoutMessage>(body);

            if (basketCheckoutMessage == null)
            {
                await args.DeadLetterMessageAsync(args.Message, "DeserializationFailed", "BasketCheckoutMessage was null after deserialization");
                return;
            }

            if (_authMode != "None")
            {
                var token = basketCheckoutMessage.SecurityContext?.AccessToken;

                if (string.IsNullOrWhiteSpace(token))
                {
                    await args.DeadLetterMessageAsync(args.Message, "MissingToken", "SecurityContext.AccessToken missing");
                    return;
                }
                var tokenValidationService = scope.ServiceProvider.GetRequiredService<TokenValidationService>();
                if (!await tokenValidationService.ValidateTokenAsync(token, args.Message.EnqueuedTime))
                {
                    //advance to the next message in the service bus
                    await args.DeadLetterMessageAsync(args.Message, "InvalidToken", "Token validation failed");  
                    return;
                }
            }

            try
            {
                //Get or Add customer
                Customer customer = await customerRepository.GetCustomerById(basketCheckoutMessage.UserId);
                if(customer == null)
                {
                    // create new customer
                    Customer newCustomer = new Customer
                    {
                        CustomerId = basketCheckoutMessage.UserId,
                        FirstName = basketCheckoutMessage.FirstName,
                        LastName = basketCheckoutMessage.LastName,
                        Email = basketCheckoutMessage.Email,
                        Address = basketCheckoutMessage.Address,
                        ZipCode = basketCheckoutMessage.ZipCode,
                        City = basketCheckoutMessage.City,
                        Country = basketCheckoutMessage.Country
                    };

                    await customerRepository.AddCustomer(newCustomer);
                }

                // Create new order object
                Guid orderId = Guid.NewGuid();
                Order order = new Order
                {
                    UserId = basketCheckoutMessage.UserId,
                    Id = orderId,
                    OrderPaid = false,
                    OrderPlaced = DateTime.UtcNow,
                    OrderTotal = basketCheckoutMessage.BasketTotal
                };

                order.OrderLines = new List<OrderLine>();

                // create OrderLines for each basketLine (event tickets)
                foreach(var bLine in basketCheckoutMessage.BasketLines)
                {
                    OrderLine orderLine = new OrderLine
                    {
                        OrderLineId = Guid.NewGuid(),
                        Price = bLine.Price,
                        TicketAmount = bLine.TicketAmount,
                        EventId = bLine.EventId,
                        EventName = bLine.EventName,
                        EventDate = bLine.EventDate,
                        VenueName = bLine.VenueName,
                        VenueCity = bLine.VenueCity,
                        VenueCountry = bLine.VenueCountry
                    };
                    order.OrderLines.Add(orderLine);
                }

                await orderRepository.AddOrder(order);
                Console.WriteLine($"[Checkout] Order {order.Id} added for user {order.UserId}");

                var orderPaymentRequestMessage = new OrderPaymentRequestMessage
                {
                    Id = Guid.NewGuid(),
                    CardExpiration = basketCheckoutMessage.CardExpiration,
                    CardName = basketCheckoutMessage.CardName,
                    CardNumber = basketCheckoutMessage.CardNumber,
                    OrderId = orderId,
                    Total = basketCheckoutMessage.BasketTotal
                };

                await _messageBus.PublishMessage(orderPaymentRequestMessage, orderPaymentRequestMessageTopic, "OrderPaymentRequest.v1");

                //Mark as processed
                await processedRepository.MarkAsProcessedAsync(messageId);
                //Complete message
                await args.CompleteMessageAsync(args.Message);
                Console.WriteLine($"[Checkout] Message {args.Message.MessageId} completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Checkout] Error processing {messageId}: {ex.Message}");
                throw; // let Service Bus retry
            }
        }

        private async Task OnOrderPaymentUpdateReceived(ProcessMessageEventArgs args)
        {
            Console.WriteLine($"[PaymentUpdate] Received message: {args.Message.MessageId}");

            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            var body = args.Message.Body.ToString();
            var orderPaymentUpdateMessage = JsonConvert.DeserializeObject<OrderPaymentUpdateMessage>(body);

            if (orderPaymentUpdateMessage == null)
            {
                Console.WriteLine("[PaymentUpdate] Received null or invalid message.");
                await args.AbandonMessageAsync(args.Message);
                return;
            }

            try
            {
                await orderRepository.UpdateOrderPaymentStatus(
                    orderPaymentUpdateMessage.OrderId,
                    orderPaymentUpdateMessage.PaymentSuccess
                );

                Console.WriteLine($"[PaymentUpdate] Order {orderPaymentUpdateMessage.OrderId} payment status updated to {orderPaymentUpdateMessage.PaymentSuccess}");
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentUpdate][ERROR] Failed to update payment status for order {orderPaymentUpdateMessage.OrderId}: {ex}");
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private Task OnErrorReceived(ProcessErrorEventArgs args)
        {
            Console.WriteLine("[ServiceBusConsumer][ERROR] Service Bus exception occurred:");
            Console.WriteLine($"Exception Message: {args.Exception.Message}");
            Console.WriteLine($"Error Source: {args.ErrorSource}");
            Console.WriteLine($"Entity Path: {args.EntityPath}");
            Console.WriteLine($"Fully Qualified Namespace: {args.FullyQualifiedNamespace}");
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_started) return;
            _started = false;

            await _checkoutProcessor.StopProcessingAsync();
            await _paymentUpdateProcessor.StopProcessingAsync();

            await _checkoutProcessor.DisposeAsync();
            await _paymentUpdateProcessor.DisposeAsync();

            await _client.DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }

}
