using AutoMapper;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Domain.Entities.Restaurants;

namespace ScanToOrder.Application.Mappings
{
    public class GeneralProfile : Profile
    {
        public GeneralProfile()
        {
            CreateMap<Restaurant, RestaurantDto>().ReverseMap();
        }
    }
}
