using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GloboTicket.Services.ShoppingBasket.Profiles
{
    public class BasketLineProfile : Profile
    {
        public BasketLineProfile()
        {
            CreateMap<Models.BasketLineForCreation, Entities.BasketLine>();
            CreateMap<Models.BasketLineForUpdate, Entities.BasketLine>();
            CreateMap<Entities.BasketLine, Models.BasketLine>().ReverseMap();

            // CreateMap<Models.BasketLineForCreation, Entities.BasketLine>()
            // .ForMember(dest => dest.BasketId, opt => opt.Ignore()) // don’t let AutoMapper overwrite this
            // .ForMember(dest => dest.BasketLineId, opt => opt.Ignore()); // repo will set this

            // CreateMap<Models.BasketLineForUpdate, Entities.BasketLine>()
            //     .ForMember(dest => dest.BasketId, opt => opt.Ignore())
            //     .ForMember(dest => dest.BasketLineId, opt => opt.Ignore());

            // CreateMap<Entities.BasketLine, Models.BasketLine>().ReverseMap();

        }
    }
}
