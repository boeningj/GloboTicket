using AutoMapper;
using GloboTicket.Services.EventCatalog.Repositories;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GloboTicket.Integration.MessagingBus;
using GloboTicket.Services.EventCatalog.Messages;
using GloboTicket.Services.EventCatalog.Models;

using GloboTicket.Services.EventCatalog.Entities;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using GloboTicket.Services.EventCatalog.Models.V2;

namespace GloboTicket.Services.EventCatalog.Controllers
{
    [ApiVersion("1.0")]
    //[ApiVersion("2.0")]  // Only needed for "Version Interleaving"
    [Route("api/events")]
    [Route("api/v{version:apiVersion}/events")]
    [ApiController]
    //Comment out the below Authorize attribute when using the Ocelot Gateway
    [Authorize(Policy = "CanRead")]
    public class EventController : ControllerBase
    {
        //private readonly IEventRepository eventRepository;
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IMessageBus messageBus;

        public EventController(IUnitOfWork unitOfWork, IMapper mapper, IMessageBus messageBus)
        {
            //this.eventRepository = eventRepository;
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.messageBus = messageBus;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Models.EventDto>>> Get(
            [FromQuery] Guid categoryId)
        {
            //var result = await eventRepository.GetEvents(categoryId);
            var result = await unitOfWork.EventRepository.GetEvents(categoryId);
            return Ok(mapper.Map<List<Models.EventDto>>(result));
        }

        [HttpGet("{eventId}")]        
        public async Task<ActionResult<Models.EventDto>> GetById(Guid eventId)
        {
            //var result = await eventRepository.GetEventById(eventId);
            var result = await unitOfWork.EventRepository.GetEventById(eventId);
            return Ok(mapper.Map<Models.EventDto>(result));
        }

        /* This is the 'Version Interleaving' method which is useful if you only have one endpoint that needs multiple versions.
        Since all of our GET endpoints will have new versions we'll move them into a EventControllerV2.cs
        [HttpGet]
        [MapToApiVersion("2.0")]
        public async Task<ActionResult<IEnumerable<Models.V2.EventDto>>> GetV2(
            [FromQuery] Guid categoryId)
        {
            var result = await unitOfWork.EventRepository.GetEvents(categoryId);
            return Ok(mapper.Map<List<Models.V2.EventDto>>(result));
        }

        [HttpGet("{eventId}")]
        [MapToApiVersion("2.0")]
        public async Task<ActionResult<Models.V2.EventDto>> GetByIdV2(Guid eventId)
        {
            var result = await unitOfWork.EventRepository.GetEventById(eventId);
            return Ok(mapper.Map<Models.V2.EventDto>(result));
        }
        */

        // [HttpPost("eventpriceupdate")]
        // public async Task<ActionResult<PriceUpdate>> Post(PriceUpdate priceUpdate)
        // {
        //     var eventToUpdate = await eventRepository.GetEventById(priceUpdate.EventId);
        //     eventToUpdate.Price = priceUpdate.Price;
        //     await eventRepository.SaveChanges();

        //     //send integration event on to service bus

        //     PriceUpdatedMessage priceUpdatedMessage = new PriceUpdatedMessage
        //     {
        //         EventId = priceUpdate.EventId,
        //         Price = priceUpdate.Price
        //     };

        //     try
        //     {
        //         await messageBus.PublishMessage(priceUpdatedMessage, "priceupdatedmessage");
        //     }
        //     catch (Exception e)
        //     {
        //         Console.WriteLine(e);
        //         throw;
        //     }


        //     return Ok(priceUpdate);
        // }

        [Authorize(Policy = "CanWrite")]
        [HttpPost("eventupdate")]
        public async Task<ActionResult<EventUpdateDto>> Post(EventUpdateDto eventUpdate)
        {
            var eventToUpdate = await unitOfWork.EventRepository.GetEventById(eventUpdate.EventId);

            eventToUpdate.Name = eventUpdate.Name;
            eventToUpdate.Price = eventUpdate.Price;
            eventToUpdate.Date = eventUpdate.Date;
            // message property is only for the message to other microservices

            // structure message to be sent to service bus
            EventUpdatedMessage eventUpdatedMessage = new EventUpdatedMessage
            {
                Id = Guid.NewGuid(),
                CreationDateTime = DateTime.UtcNow,
                EventId = eventUpdate.EventId,
                Name = eventUpdate.Name,
                Date = eventUpdate.Date,
                Price = eventUpdate.Price,
                Message = eventUpdate.Message
            };

            // serialize message for storage in database table text field
            var jsonMessage = JsonConvert.SerializeObject(eventUpdatedMessage);

            IntegrationEventLog logEntry = new IntegrationEventLog
            {
                IntegrationEventLogId = Guid.NewGuid(), //required by CosmosDB
                // Use this field to version your integration events when the message contract changes
                // (e.g., TicketedEventChangeV2 → EventUpdatedMessageV2)
                IntegrationEventType = "TicketedEventChange.v1",
                // Use this field to route events to a new topic (e.g., eventupdatedmessage-v2)
                // when you want hard isolation or consumers upgrade at different speeds
                ServiceBusTopicName = "eventupdatedmessage",                
                IntegrationEventBody = jsonMessage,
                State = "Pending",
                PartitionKey = "IntegrationEventPublisher"
            };

            unitOfWork.IntegrationEventLogRepository.AddEventLogEntry(logEntry);

            try
            {
                await unitOfWork.CommitAsync();
                
                //OLD - WE NO LONGER PUBLISH directly from here - The new IntegrationEventPublisher will handle publishing
                //await messageBus.PublishMessage(eventUpdatedMessage, "eventupdatedmessage");
                // Mark the event as successfully published
                //logEntry.State = "Published";
                //unitOfWork.IntegrationEventLogRepository.UpdateEventLogEntry(logEntry);
                //await unitOfWork.CommitAsync();
            }
            catch (Exception e)
            {
                unitOfWork.Rollback();

                Console.WriteLine(e);
                throw;
            }

            return Ok(eventUpdate);
        }
    }
}