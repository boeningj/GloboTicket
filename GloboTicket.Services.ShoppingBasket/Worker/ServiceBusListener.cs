using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.ShoppingBasket.Models;
using GloboTicket.Services.ShoppingBasket.Repositories;
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

namespace GloboTicket.Services.ShoppingBasket.Worker
{
    // public class ServiceBusListener : IHostedService
    // {
    //     private readonly IConfiguration configuration;
    //     private ISubscriptionClient subscriptionClient;
    //     private readonly BasketLinesIntegrationRepository basketLinesRepository;

    //     public ServiceBusListener(IConfiguration configuration, BasketLinesIntegrationRepository basketLinesRepository)
    //     {
    //         this.configuration = configuration;
    //         this.basketLinesRepository = basketLinesRepository;
    //     }

    //     public Task StartAsync(CancellationToken cancellationToken)
    //     {
    //         subscriptionClient = new SubscriptionClient(configuration.GetValue<string>("ServiceBusConnectionString"), configuration.GetValue<string>("PriceUpdatedMessageTopic"), configuration.GetValue<string>("subscriptionName"));

    //         var messageHandlerOptions = new MessageHandlerOptions(e =>
    //         {
    //             ProcessError(e.Exception);
    //             return Task.CompletedTask;
    //         })
    //         {
    //             MaxConcurrentCalls = 3,
    //             AutoComplete = false
    //         };

    //         subscriptionClient.RegisterMessageHandler(ProcessMessageAsync, messageHandlerOptions);

    //         return Task.CompletedTask;
    //     }

    //     private async Task ProcessMessageAsync(Message message, CancellationToken token)
    //     {
    //         var messageBody = Encoding.UTF8.GetString(message.Body);
    //         PriceUpdate priceUpdate = JsonConvert.DeserializeObject<PriceUpdate>(messageBody);

    //         await basketLinesRepository.UpdatePricesForIntegrationEvent(priceUpdate);

    //         await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);

    //     }

    //     public async Task StopAsync(CancellationToken cancellationToken)
    //     {
    //         await this.subscriptionClient.CloseAsync();
    //     }

    //     protected void ProcessError(Exception e)
    //     {
    //     }
    // }


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
            var topicName = _configuration.GetValue<string>("PriceUpdatedMessageTopic");
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
            var body = Encoding.UTF8.GetString(args.Message.Body);
            var priceUpdate = JsonConvert.DeserializeObject<PriceUpdate>(body);

            // Create scope for BasketLinesIntegrationRepository
            // This avoids DbContext lifetime issues by doing this here vs. injecting a singleton through DI
            // AddScoped ensures that whenever your ServiceBusListener creates a scope (_serviceProvider.CreateScope()),
            // the BasketLinesIntegrationRepository inside it will get a new DbContext instance.
            // If it were AddSingleton, you’d have a single long-lived DbContext, which would cause connection leaks,
            // stale tracking, and threading issues.
            using var scope = _serviceProvider.CreateScope();
            var basketLinesRepository = scope.ServiceProvider.GetRequiredService<BasketLinesIntegrationRepository>();

            await basketLinesRepository.UpdatePricesForIntegrationEvent(priceUpdate);

            await args.CompleteMessageAsync(args.Message);
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            Console.WriteLine($"Message handler encountered an exception {args.Exception}.");
            Console.WriteLine($"ErrorSource: {args.ErrorSource}");
            return Task.CompletedTask;
        }        
        
    }
}
