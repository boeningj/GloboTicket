using AutoMapper;
using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Services.Ordering.Profiles
{
    public class OrderLineProfile : Profile
    {
        public OrderLineProfile()
        {
            CreateMap<Entities.OrderLine, OrderLineDto>().ReverseMap();
        }
    }
}
