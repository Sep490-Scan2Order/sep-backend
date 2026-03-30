using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
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
                .ForMember(dest => dest.AccountId, opt => opt.Ignore());

            CreateMap<Tenant, TenantDto>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Account.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.Account.Phone))
                .ForMember(dest => dest.Avatar, opt => opt.MapFrom(src => src.Account.Avatar))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Account.Role.ToString()))
                .ForMember(dest => dest.Verified, opt => opt.MapFrom(src => src.Account.Verified))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Account.IsActive))
                
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

            CreateMap<Staff, StaffDto>()
        .ForMember(dest => dest.RestaurantName,
            opt => opt.MapFrom(src => src.Restaurant.RestaurantName))
        .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Account.Email))
        .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Account.Role.ToString()));
            CreateMap<AuthenticationUser, AdminDto>();

            CreateMap<CreateCategoryRequest, Category>();
            CreateMap<UpdateCategoryRequest, Category>();
            CreateMap<Category, CategoryDto>();

            CreateMap<CreateDishRequest, Dish>();
            CreateMap<UpdateDishRequest, Dish>();
            CreateMap<Dish, DishDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.CategoryName));

            CreateMap<CreateTemplateRequestDto, MenuTemplate>();
            CreateMap<MenuTemplate, CreateTemplateResponseDto>();
            CreateMap<MenuTemplate, MenuTemplateDto>();

            CreateMap<MenuRestaurant, MenuRestaurantDto>();
            CreateMap<CreateMenuRestaurantRequestDto, MenuRestaurant>()
                .ForMember(dest => dest.MenuTemplateId, opt => opt.MapFrom(src => src.TemplateId));

            CreateMap<Restaurant, RestaurantDto>()
                .ForMember(dest => dest.Longitude,
                    opt => opt.MapFrom(src => src.Location != null ? (decimal?)src.Location.X : null))
                .ForMember(dest => dest.Latitude,
                    opt => opt.MapFrom(src => src.Location != null ? (decimal?)src.Location.Y : null));
            CreateMap<MenuTemplate, MenuTemplateDto>();
            CreateMap<MenuRestaurant, MenuRestaurantDto>();      

            CreateMap<CartModel, CartDto>();
            CreateMap<CartItemModel, CartItemModel>();

            CreateMap<OrderDetail, CustomerOrderDetailDto>()
                .ForMember(dest => dest.DishName,
                    opt => opt.MapFrom(src => src.Dish != null ? src.Dish.DishName : string.Empty))
                .ForMember(dest => dest.ImageUrl,
                    opt => opt.MapFrom(src => src.Dish != null ? src.Dish.ImageUrl : string.Empty));

            CreateMap<Order, CustomerOrderSummaryDto>()
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RestaurantName,
                    opt => opt.MapFrom(src => src.Restaurant != null ? src.Restaurant.RestaurantName : string.Empty))
                .ForMember(dest => dest.UpdatedAt,
                    opt => opt.MapFrom(src => src.UpdatedAt ?? src.CreatedAt))
                .ForMember(dest => dest.IsRefundLog,
                    opt => opt.MapFrom(src => src.typeOrder == TypeOrder.Refund));

            CreateMap<Restaurant, TenantRestaurantRevenueDto>()
                .ForMember(dest => dest.RestaurantId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.RestaurantName, opt => opt.MapFrom(src => src.RestaurantName))
                .ForMember(dest => dest.CurrentPlan, opt => opt.MapFrom(src =>
                    src.Subscription != null
                    && src.Subscription.Status == SubscriptionStatus.Active
                    && src.Subscription.Plan != null
                        ? src.Subscription.Plan.Name
                        : "No Active Plan"))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive ?? false));
        }
    }
}