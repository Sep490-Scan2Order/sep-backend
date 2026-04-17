using AutoMapper;
using NetTopologySuite.Geometries;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.Mappings
{
    public class GeneralProfile : Profile
    {
        public GeneralProfile()
        {
            // Restaurant mapping with custom logic for Location to Latitude and Longitude
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

            // Plan mapping
            CreateMap<Plan, PlanResponse>().ReverseMap();
            CreateMap<PlanFeaturesConfig, PlanFeaturesResponse>().ReverseMap();
            CreateMap<CreatePlanRequest, Plan>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => PlanStatus.Active))
                .ForMember(dest => dest.DailyRateMonth, opt => opt.MapFrom(src => src.DurationInDays > 0 ? src.MonthlyPrice / src.DurationInDays : 0))
                .ForMember(dest => dest.DailyRateYear, opt => opt.MapFrom(src => src.DurationInDays > 0 ? src.YearlyPrice / (src.DurationInDays * 12) : 0));
            CreateMap<CreatePlanFeaturesRequest, PlanFeaturesConfig>();
            CreateMap<UpdatePlanRequest, Plan>()
                .ForMember(dest => dest.DailyRateMonth, opt => opt.MapFrom(src => src.DurationInDays > 0 ? src.MonthlyPrice / src.DurationInDays : 0))
                .ForMember(dest => dest.DailyRateYear, opt => opt.MapFrom(src => src.DurationInDays > 0 ? src.YearlyPrice / (src.DurationInDays * 12) : 0));
            CreateMap<UpdatePlanFeaturesRequest, PlanFeaturesConfig>();

            CreateMap<Configurations, ConfigurationResponse>();

            // Mapping for BranchDishConfig with custom logic to include related Restaurant and Dish information
            CreateMap<CreateRestaurantRequest, Restaurant>()
                .ForMember(dest => dest.Location, opt => opt.MapFrom(src =>
                    (src.Latitude.HasValue && src.Longitude.HasValue)
                        ? new Point(src.Longitude.Value, src.Latitude.Value) { SRID = 4326 }
                        : null))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.IsOpened, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.IsReceivingOrders, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.TotalOrder, opt => opt.MapFrom(src => 0))
                .ForMember(dest => dest.QrMenu, opt => opt.MapFrom(src => $"https://scantoorder.com/menu/{Guid.NewGuid()}"))
                .ForMember(dest => dest.OpenTime, opt => opt.Ignore())
                .ForMember(dest => dest.CloseTime, opt => opt.Ignore());


            CreateMap<BranchDishConfig, BranchDishConfigDto>()
                .ForMember(dest => dest.RestaurantName,
                    opt => opt.MapFrom(src => src.Restaurant.RestaurantName))
                .ForMember(dest => dest.DishName,
                    opt => opt.MapFrom(src => src.Dish.DishName))
                .ForMember(dest => dest.DishImageUrl,
                    opt => opt.MapFrom(src => src.Dish.ImageUrl));

            CreateMap<CreateBranchDishConfig, BranchDishConfig>();

            // Promotion mapping with custom logic for default values and conditional mapping
            CreateMap<CreatePromotionDto, Promotion>()
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.IsDeleted, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.Priority, opt => opt.Ignore())
                .ForSourceMember(src => src.DishIds, opt => opt.DoNotValidate())
                .ForSourceMember(src => src.RestaurantIds, opt => opt.DoNotValidate());

            CreateMap<Promotion, PromotionResponseDto>()
                .ForMember(dest => dest.DishIds, opt => opt.MapFrom(src =>
                    src.PromotionDishes.Select(pd => pd.DishId).ToList()))
                .ForMember(dest => dest.RestaurantIds, opt => opt.MapFrom(src =>
                    src.RestaurantPromotions.Select(rp => rp.RestaurantId).ToList()));

            CreateMap<UpdatePromotionDto, Promotion>()
                .IncludeBase<CreatePromotionDto, Promotion>()
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

            CreateMap<Shift, ShiftDto>()
                .ForMember(dest => dest.StaffName, opt => opt.MapFrom(src => src.Staffs != null ? src.Staffs.Name : string.Empty));

            CreateMap<ShiftReport, ShiftReportDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.Shift != null && src.Shift.Staffs != null ? src.Shift.Staffs.Name : string.Empty))
                .ForMember(dest => dest.ExpectedTotalAmount, opt => opt.MapFrom(src => src.Shift != null ? src.Shift.OpeningCashAmount + src.TotalCashOrder + src.TotalTransferOrder : 0));


        }
    }
}