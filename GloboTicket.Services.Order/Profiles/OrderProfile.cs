using AutoMapper;
using GloboTicket.Gateway.Shared.Order;

namespace GloboTicket.Services.Ordering.Profiles
{
    public class OrderProfile : Profile
    {
        public OrderProfile()
        {
            CreateMap<Entities.Order, OrderDto>().ReverseMap();
        }
    }
}
