using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Payment.Messages;
using GloboTicket.Services.Payment.Model;
using GloboTicket.Services.Payment.Services;
//using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;

namespace GloboTicket.Services.Payment.Worker
{
    public class ServiceBusListener : IHostedService, IAsyncDisposable
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IExternalGatewayPaymentService externalGatewayPaymentService;
        private readonly IMessageBus messageBus;
        private readonly string orderPaymentUpdatedMessageTopic;

        private ServiceBusClient client;
        private ServiceBusProcessor subscriptionClient;

        public ServiceBusListener(IConfiguration configuration, ILoggerFactory loggerFactory,
            IExternalGatewayPaymentService externalGatewayPaymentService, IMessageBus messageBus)
        {
            logger = loggerFactory.CreateLogger<ServiceBusListener>();
            orderPaymentUpdatedMessageTopic = configuration.GetValue<string>("OrderPaymentUpdatedMessageTopic");

            this.configuration = configuration;
            this.externalGatewayPaymentService = externalGatewayPaymentService;
            this.messageBus = messageBus;

            var serviceBusConnectionString = configuration.GetValue<string>("ServiceBusConnectionString");
            var topicName = configuration.GetValue<string>("OrderPaymentRequestMessageTopic");

            client = new ServiceBusClient(serviceBusConnectionString);

            subscriptionClient = client.CreateProcessor(topicName, configuration.GetValue<string>("subscriptionName"), new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 3,
                AutoCompleteMessages = false
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            subscriptionClient.ProcessMessageAsync += ProcessMessageAsync;
            subscriptionClient.ProcessErrorAsync += ProcessErrorAsync;

            await subscriptionClient.StartProcessingAsync(cancellationToken);
            logger.LogDebug("ServiceBusListener started.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug("ServiceBusListener stopping.");

            if (subscriptionClient != null)
            {
                await subscriptionClient.StopProcessingAsync(cancellationToken);
                await subscriptionClient.DisposeAsync();
            }

            if (client != null)
            {
                await client.DisposeAsync();
            }
        }

        //Both ServiceBusClient and ServiceBusProcessor implement IAsyncDisposable.
        //While they're already being disposed in StopAsync above, implementing IAsyncDisposable is
        //the recommended best practice because:  It ensures cleanup still happens even if the host is
        //shut down without calling StopAsync.
        //ASP.NET Core’s DI system will still automatically call DisposeAsync() on your hosted service during shutdown.
        public async ValueTask DisposeAsync()
        {
            if (subscriptionClient != null)
                await subscriptionClient.DisposeAsync();
            if (client != null)
                await client.DisposeAsync();
        }

        protected async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            var messageBody = args.Message.Body.ToString();
            // Read the messageType from the ApplicationProperties header
            var messageType = args.Message.ApplicationProperties.TryGetValue("MessageType", out var value) ? value?.ToString() : null;
            if (string.IsNullOrWhiteSpace(messageType))
            {
                await args.DeadLetterMessageAsync(args.Message, "MissingMessageType", "MessageType header is missing. Cannot determine how to process message.");                
                return;
            }

            switch (messageType)
            {
                case "OrderPaymentRequest.v1":
                {
                    var orderPaymentRequestMessage = JsonConvert.DeserializeObject<OrderPaymentRequestMessage>(messageBody)
                        ?? throw new InvalidOperationException("Failed to deserialize OrderPaymentRequest.v1");

                    PaymentInfo paymentInfo = new PaymentInfo
                    {
                        CardNumber = orderPaymentRequestMessage.CardNumber,
                        CardName = orderPaymentRequestMessage.CardName,
                        CardExpiration = orderPaymentRequestMessage.CardExpiration,
                        Total = orderPaymentRequestMessage.Total
                    };

                    var result = await externalGatewayPaymentService.PerformPayment(paymentInfo);

                    var orderPaymentUpdateMessage = new OrderPaymentUpdateMessage
                    {
                        PaymentSuccess = result,
                        //PaymentSuccess = true,
                        OrderId = orderPaymentRequestMessage.OrderId
                    };

                    try
                    {
                        await messageBus.PublishMessage(orderPaymentUpdateMessage, orderPaymentUpdatedMessageTopic);
                        await args.CompleteMessageAsync(args.Message);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error publishing payment update message.");
                        throw;
                    }

                    logger.LogDebug($"{orderPaymentRequestMessage.OrderId}: ServiceBusListener received item.");
                    await Task.Delay(20000); // simulate processing delay
                    logger.LogDebug($"{orderPaymentRequestMessage.OrderId}: ServiceBusListener processed item.");
                    
                    break;
                }
                case "OrderPaymentRequest.v2":
                {
                    // Placeholder for v2 processing
                    // var orderPaymentRequestMessageV2 = JsonConvert.DeserializeObject<OrderPaymentRequestMessageV2>(messageBody);
                    // Process the payment, publish update, etc.
                    break;
                }
                default:
                {
                    // Dead-letter unknown message types
                    await args.DeadLetterMessageAsync(args.Message, "UnknownMessageType", $"Unsupported message type: {messageType}");                    
                    return;
                }
            }
        }

        protected Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception, "Error while processing message in ServiceBusListener.");
            return Task.CompletedTask;
        }        

    }
}
