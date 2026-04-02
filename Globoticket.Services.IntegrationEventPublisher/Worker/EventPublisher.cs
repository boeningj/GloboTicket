using Globoticket.Services.IntegrationEventPublisher.DbContexts;
using Globoticket.Services.IntegrationEventPublisher.Entities;
using Globoticket.Services.IntegrationEventPublisher.Messages;
using Globoticket.Services.IntegrationEventPublisher.Repositories;
using GloboTicket.Integration.Messages;
using GloboTicket.Integration.MessagingBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace Globoticket.Services.IntegrationEventPublisher.Worker
{
    public class EventPublisher : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration configuration;
        private readonly IMessageBus messageBus;
        private readonly string eventChangedMessageTopic;
        //_repository is registered as a scoped service in Startup.cs, but EventPublisher is a registered Hosted Service, which are
        //always singletons in ASP.NET Core.  You can't inject a scoped service into a singleton because the singleton lives for
        //the lifetime of the application, while the scoped service is meant to live only for one request (or one DI scope).
        //Inject an IServiceProvider instead & use it to create a scope inside the background service.  This is the most common
        //pattern for background workers that need scoped services (like a DbContext).
        //private readonly IIntegrationEventRepository _repository;

        private readonly IServiceProvider _serviceProvider;

        public EventPublisher(IConfiguration configuration, ILoggerFactory loggerFactory, IMessageBus messageBus, IServiceProvider serviceProvider) //IIntegrationEventRepository repository)
        {
            _logger = loggerFactory.CreateLogger<EventPublisher>();
            eventChangedMessageTopic = configuration.GetValue<string>("EventChangedMessageTopic");
            this.configuration = configuration;
            this.messageBus = messageBus;
            //_repository = repository;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("EventPublisher is starting.");

            stoppingToken.Register(() => _logger.LogDebug("EventPublisher background task is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("EventPublisher background task is doing background work.");
                try
                {
                    await CheckEventsForPublishing();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in EventPublisher loop");
                }                
                await Task.Delay(configuration.GetValue<int>("CheckUpdateTime"), stoppingToken);                
            }

            _logger.LogDebug("EventPublisher background task is stopping.");

            await Task.CompletedTask;
        }

        /* SQL Server way (not Cosmos DB-optimized friendly)
        private async Task CheckEventsForPublishing()
        {
            _logger.LogDebug("Checking for unpublished events");

            var unpublishedEvents = await _repository.GetUnpublishedEvents();

            foreach (var eventToPublish in unpublishedEvents)
            {
                _logger.LogInformation("----- Publishing integration event: {IntegrationEventId} to Service Bus Topic: {ServiceBusTopicName}", eventToPublish.IntegrationEventLogId, eventToPublish.ServiceBusTopicName);
                try
                {
                    var message = JsonConvert.DeserializeObject<EventUpdatedMessage>(eventToPublish.IntegrationEventBody);

                    await _repository.UpdateIntegrationEventLogEntryState(eventToPublish, "In Process");

                    await messageBus.PublishMessage(message, eventToPublish.ServiceBusTopicName);

                    await _repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Published");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("----- Error publishing integration event: {IntegrationEventId}.  Exception:{ex}", eventToPublish.IntegrationEventLogId, ex.ToString());
                }
            }
        }
        */

        private async Task CheckEventsForPublishing()
        {
            try
            {
                _logger.LogDebug("Checking for unpublished events");

                //var unpublishedEvents = await _repository.GetUnpublishedEvents();
                //Use the injected IServiceProvider to create a new DI scope in each method call
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();

                var unpublishedEvents = await repository.GetUnpublishedEvents();

                _logger.LogInformation("Found {Count} unpublished events", unpublishedEvents.Count());

                foreach (var eventToPublish in unpublishedEvents)
                {
                    _logger.LogInformation(
                        "Publishing integration event: {IntegrationEventId} to Service Bus Topic: {ServiceBusTopicName}",
                        eventToPublish.IntegrationEventLogId, eventToPublish.ServiceBusTopicName);

                    try
                    {
                        var message = JsonConvert.DeserializeObject<EventUpdatedMessage>(eventToPublish.IntegrationEventBody);

                        //_repository.UpdateIntegrationEventLogEntryState(eventToPublish, "In Process");
                        repository.UpdateIntegrationEventLogEntryState(eventToPublish, "In Process");
                        // NOTE:  If this service ever scales to multiple instances (e.g. Azure App Service scale-out or 
                        // Kubernetes replicas), then you need to call _repository.SaveChangesAsync() immediately after
                        // changing the state to "In Process" above.  It will result in an extra write\network call to
                        // Cosmos DB, but will prevent publishing duplicate messages since there will be no way for
                        // multiple running instances to select the same unpublished event via GetUnpublishedEvents() above.  

                        await messageBus.PublishMessage(message, eventToPublish.ServiceBusTopicName, eventToPublish.IntegrationEventType);

                        //_repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Published");
                        repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Published");
                    }
                    catch (CosmosException cex)
                    {
                        if (cex.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.LogError(
                                "Cosmos 404 updating IntegrationEventLog {IntegrationEventId}. " +
                                "Likely partition key mismatch or concurrency conflict. Error: {Error}",
                                eventToPublish.IntegrationEventLogId,
                                cex.ToString());

                            repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Failed");
                        }
                        else
                        {
                            throw; // let other Cosmos errors bubble up
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error publishing integration event: {IntegrationEventId}", eventToPublish.IntegrationEventLogId);

                        //_repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Failed");
                        repository.UpdateIntegrationEventLogEntryState(eventToPublish, "Failed");
                    }
                }            

                // commit all state changes in one batch                
                if (unpublishedEvents.Any())
                {
                    //await _repository.SaveChangesAsync();
                    await repository.SaveChangesAsync();
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error while checking for unpublished events");
            }
        }

    }
}
