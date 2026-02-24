using AutoMapper;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
         
            CreateMap<RegisterTenantRequest, AuthenticationUser>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => Role.Tenant)) 
                .ForMember(dest => dest.Verified, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.Password, opt => opt.Ignore());

            CreateMap<RegisterTenantRequest, Tenant>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.TotalRestaurants, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.TotalDishes, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.TotalCategories, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.AccountId, opt => opt.Ignore());

            CreateMap<Tenant, TenantDto>()
           .ForMember(dest => dest.PlanName, opt => opt.MapFrom(src =>
               src.Subscriptions
                   .Where(s => s.IsActive)
                   .OrderByDescending(s => s.StartDate)
                   .Select(s => s.Plan.Name)
                   .FirstOrDefault() ?? "Chưa mua gói"
           ));
        }
    }
}