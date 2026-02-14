using AutoMapper;
using NetTopologySuite.Geometries;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Application.Mappings
{
    public class GeneralProfile : Profile
    {
        public GeneralProfile()
        {
            CreateMap<Restaurant, RestaurantDto>()
                .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src =>
                    src.Location != null ? (decimal)src.Location.X : (decimal?)null))
                .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src =>
                    src.Location != null ? (decimal)src.Location.Y : (decimal?)null))

                .ReverseMap()
                .ForMember(dest => dest.Location, opt => opt.MapFrom(src =>
                    (src.Longitude.HasValue && src.Latitude.HasValue)
                    ? new Point((double)src.Longitude.Value, (double)src.Latitude.Value) { SRID = 4326 }
                    : null));

            CreateMap<Plan, PlanDto>().ReverseMap();
        }
    }
}
