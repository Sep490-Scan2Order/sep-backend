using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Dishes;
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
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Account.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Account.Phone))
                .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => src.Account.Avatar))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Account.Role.ToString()))
                .ForMember(dest => dest.Verified, opt => opt.MapFrom(src => src.Account.Verified))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Account.IsActive))
                .ForMember(dest => dest.PlanName, opt => opt.MapFrom(src =>
                    src.Subscriptions
                        .Where(s => s.IsActive)
                        .OrderByDescending(s => s.StartDate)
                        .Select(s => s.Plan.Name)
                        .FirstOrDefault() ?? "Chưa mua gói"
                ))
                .ForMember(dest => dest.BankName,
                    opt => opt.MapFrom(src => src.Bank != null ? src.Bank.Name : string.Empty))
                .ForMember(dest => dest.BankLogo,
                    opt => opt.MapFrom(src => src.Bank != null ? src.Bank.LogoUrl : string.Empty));

            CreateMap<UpdateTenantDtoRequest, Tenant>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());

            CreateMap<CreateStaffRequest, AuthenticationUser>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Phone))
                .ForMember(dest => dest.Verified, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(_ => Role.Staff));

            CreateMap<CreateStaffRequest, Staff>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.RestaurantId));

            CreateMap<Staff, StaffDto>();
            CreateMap<AuthenticationUser, AdminDto>();

            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryDto>();

            CreateMap<CreateDishRequest, Dish>();
            CreateMap<UpdateDishRequest, Dish>();
            CreateMap<Dish, DishDto>();

        }
    }
}