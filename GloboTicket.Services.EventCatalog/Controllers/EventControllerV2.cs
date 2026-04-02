using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using GloboTicket.Services.EventCatalog.Models.V2;
using GloboTicket.Services.EventCatalog.Repositories;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

//Note if the namespace has the .VN then we don't need the suffix in the controller class name
namespace GloboTicket.Services.EventCatalog.Controllers.V2
{
    [ApiVersion("2.0")]
    // Commenting out the below means V2 does NOT respond to /api/events anymore.
    //[Route("api/events")]
    // It will now only responds to /api/v2/events
    [Route("api/v{version:apiVersion}/events")]
    [ApiController]
    public class EventController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;        
        private readonly IMapper _mapper;

        public EventController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventDto>>> Get([FromQuery] Guid categoryId)
        {
            var result = await _unitOfWork.EventRepository.GetEvents(categoryId);
            return Ok(_mapper.Map<List<EventDto>>(result));
        }

        [HttpGet("{eventId}")]
        public async Task<ActionResult<EventDto>> GetById(Guid eventId)
        {
            var result = await _unitOfWork.EventRepository.GetEventById(eventId);
            return Ok(_mapper.Map<EventDto>(result));
        }
    }
}