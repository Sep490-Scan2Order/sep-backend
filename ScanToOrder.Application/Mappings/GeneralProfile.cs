using AutoMapper;
using NetTopologySuite.Geometries;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.DTOs.Voucher;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Restaurant;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.Vouchers;

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
            CreateMap<Plan, PlanDto>().ReverseMap();

            // Voucher mapping with custom logic for Name and Description
            CreateMap<CreateVoucherDto, Voucher>()
                .ForMember(dest => dest.Name,
                    opt => opt.MapFrom(src => $"PHIẾU GIẢM GIÁ {src.Name} ĐỔI TỪ ĐIỂM TÍCH LŨY"))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description ?? string.Empty));

            CreateMap<Voucher, VoucherResponseDto>();

            CreateMap<MemberVoucher, RedeemVoucherResponseDto>()
                .ForMember(dest => dest.MemberVoucherId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.VoucherId, opt => opt.MapFrom(src => src.VoucherId))
                .ForMember(dest => dest.VoucherName,
                    opt => opt.MapFrom(src => src.Voucher != null ? src.Voucher.Name : string.Empty))
                .ForMember(dest => dest.DiscountValue,
                    opt => opt.MapFrom(src => src.Voucher != null ? src.Voucher.DiscountValue : 0))
                .ForMember(dest => dest.MinOrderAmount,
                    opt => opt.MapFrom(src => src.Voucher != null ? src.Voucher.MinOrderAmount : 0))
                .ForMember(dest => dest.ExpiredAt, opt => opt.MapFrom(src => src.ExpiredAt));

            // Configuration mapping
            CreateMap<Configurations, ConfigurationResponse>();

            // AddOn mapping
            CreateMap<CreateAddOnRequest, AddOn>();
            CreateMap<AddOn, AddOnDto>();
        }
    }
}