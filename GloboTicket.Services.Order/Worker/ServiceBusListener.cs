using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.Ordering.Models;
using GloboTicket.Services.Ordering.Repositories;
//using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace GloboTicket.Services.Ordering.Worker
{    
    public class ServiceBusListener : IHostedService, IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private ServiceBusProcessor _processor;
        private ServiceBusClient _client;

        public ServiceBusListener(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var connectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            var topicName = _configuration.GetValue<string>("EventUpdatedMessageTopic");
            var subscriptionName = _configuration.GetValue<string>("subscriptionName");

            _client = new ServiceBusClient(connectionString);

            _processor = _client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 3,
                AutoCompleteMessages = false
            });

            _processor.ProcessMessageAsync += ProcessMessageAsync;
            _processor.ProcessErrorAsync += ProcessErrorAsync;

            await _processor.StartProcessingAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_processor != null)
            {
                await _processor.StopProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
            }

            if (_client != null)
            {
                await _client.DisposeAsync();
            }
        }

        //Both ServiceBusClient and ServiceBusProcessor implement IAsyncDisposable.
        //While they're already being disposed in StopAsync, implementing IAsyncDisposable is the recommended best practice because:
        //It ensures cleanup still happens even if the host is shut down without calling StopAsync.
        //ASP.NET Core’s DI system will still automatically call DisposeAsync() on your hosted service during shutdown.
        public async ValueTask DisposeAsync()
        {
            if (_processor != null)
                await _processor.DisposeAsync();
            if (_client != null)
                await _client.DisposeAsync();
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            var messageId = args.Message.MessageId;
            var body = Encoding.UTF8.GetString(args.Message.Body);
            
            var messageType = args.Message.ApplicationProperties.TryGetValue("MessageType", out var value) ? value?.ToString() : null;                        
            if (string.IsNullOrWhiteSpace(messageType))
            {
                await args.DeadLetterMessageAsync(args.Message, "MissingMessageType", "MessageType header is missing. Cannot determine how to process message.");
                return;
            }
            // Create scope for BasketLinesIntegrationRepository
            // This avoids DbContext lifetime issues by doing this here vs. injecting a singleton through DI
            // AddScoped ensures that whenever your ServiceBusListener creates a scope (_serviceProvider.CreateScope()),
            // the BasketLinesIntegrationRepository inside it will get a new DbContext instance.
            // If it were AddSingleton, you’d have a single long-lived DbContext, which would cause connection leaks,
            // stale tracking, and threading issues.
            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var processedRepository = scope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();

            //Check if message was already processed
            if (await processedRepository.HasBeenProcessedAsync(messageId))
            {
                Console.WriteLine($"Skipping duplicate message {messageId}");
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            try
            {
                Console.WriteLine($"Processing message {messageId} of type {messageType}");
                switch (messageType)
                {
                    case "TicketedEventChange.v1": // V1
                    {
                        var eventUpdateV1 = JsonConvert.DeserializeObject<EventUpdate>(body) ?? throw new InvalidOperationException("Failed to deserialize EventUpdate V1");
                        //Process the message
                        await orderRepository.UpdateOrderEventInformation(eventUpdateV1);
                        break;
                    }

                    case "TicketedEventChange.v2": // V2
                    {
                        // var eventUpdateV2 = JsonConvert.DeserializeObject<EventUpdateV2>(body) ?? throw new InvalidOperationException("Failed to deserialize EventUpdate V2");;
                        //Process the message
                        // await orderRepository.UpdateOrderEventInformation(eventUpdateV2);
                        break;
                    }

                    default:
                        await args.DeadLetterMessageAsync(args.Message, "UnknownMessageType", $"Unsupported message type: {messageType}");
                        return;
                }
                
                //Mark as processed
                await processedRepository.MarkAsProcessedAsync(messageId);
                //Complete message
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message {messageId}: {ex.Message}");
                //Optional:  args.AbandonMessageAsync(args.Message);
                throw;   
            }            
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.WriteLine($"Message handler encountered an exception {args.Exception}.");
            Console.WriteLine($"ErrorSource: {args.ErrorSource}");
            return Task.CompletedTask;
        }        
    }
}
