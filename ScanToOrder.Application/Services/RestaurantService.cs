using AutoMapper;
using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Restaurant;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using SlugGenerator;

namespace ScanToOrder.Application.Services
{
    public class RestaurantService : IRestaurantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IQrCodeService _qrCodeService;
        private readonly IConfiguration _configuration;
        public RestaurantService(IUnitOfWork unitOfWork, IMapper mapper, IQrCodeService qrCodeService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _qrCodeService = qrCodeService;
            _configuration = configuration;
        }

        public async Task<RestaurantDto?> GetRestaurantByIdAsync(int id)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(id);
            if (restaurant == null)
                return null;
            var dto = _mapper.Map<RestaurantDto>(restaurant);
            return dto;
        }

        public async Task<PagedRestaurantResultDto> GetRestaurantsPagedAsync(double? latitude, double? longitude, int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 20;

            if (latitude.HasValue && longitude.HasValue)
            {
                var (items, totalCount) = await _unitOfWork.Restaurants.GetRestaurantsSortedByDistancePagedAsync(
                    latitude.Value,
                    longitude.Value,
                    page,
                    pageSize);

                var dtos = items.Select(item =>
                {
                    var dto = _mapper.Map<RestaurantDto>(item.Restaurant);
                    dto.DistanceKm = item.DistanceKm;
                    return dto;
                }).ToList();

                return new PagedRestaurantResultDto
                {
                    Items = dtos,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            var (restaurants, totalCountByOrder) = await _unitOfWork.Restaurants.GetRestaurantsSortedByTotalOrderPagedAsync(page, pageSize);
            var dtosByOrder = _mapper.Map<List<RestaurantDto>>(restaurants);

            return new PagedRestaurantResultDto
            {
                Items = dtosByOrder,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCountByOrder
            };
        }

        public async Task<List<RestaurantDto>> GetNearbyRestaurantsAsync(double latitude, double longitude, double radiusKm, int limit = 10)
        {
            var restaurantsWithDistance = await _unitOfWork.Restaurants.GetNearbyRestaurantsAsync(
                latitude,
                longitude,
                radiusKm,
                limit);

            var restaurantDtos = restaurantsWithDistance.Select(item =>
            {
                var dto = _mapper.Map<RestaurantDto>(item.Restaurant);
                dto.DistanceKm = item.DistanceKm;
                return dto;
            }).ToList();

            return restaurantDtos;
        }

        public async Task<RestaurantDto> CreateRestaurantAsync(Guid tenantId, CreateRestaurantRequest request)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null) throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);

            _ = true switch
            {
                _ when string.IsNullOrEmpty(tenant.TaxNumber) => throw new DomainException(TenantMessage.TenantError.TENANT_MISSING_TAX_NUMBER),
                _ when tenant.BankId == null || tenant.BankId == Guid.Empty => throw new DomainException(TenantMessage.TenantError.TENANT_MISSING_BANK),
                _ when string.IsNullOrEmpty(tenant.CardNumber) => throw new DomainException(TenantMessage.TenantError.TENANT_MISSING_CARD),
                _ when string.IsNullOrEmpty(request.Phone) => throw new DomainException(TenantMessage.TenantError.TENANT_MISSING_PHONE),
                _ => true
            };

            var restaurant = _mapper.Map<Restaurant>(request);
            restaurant.TenantId = tenantId;
            restaurant.IsActive = true;
            restaurant.IsOpened = false;

            string baseSlug = request.RestaurantName.GenerateSlug();
            restaurant.Slug = $"{baseSlug}#{Guid.NewGuid().ToString("N").Substring(0, 4)}";

            await _unitOfWork.Restaurants.AddAsync(restaurant);
            await _unitOfWork.SaveAsync();

            string baseUrl = _configuration["FrontEndUrl:scan2order_id_vn"]?.TrimEnd('/')!;
            restaurant.ProfileUrl = $"{baseUrl}/{restaurant.Slug}";

            var qrBytes = _qrCodeService.GenerateRestaurantQrCodeBytes(restaurant.Slug);
            restaurant.QrMenu = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}";

            _unitOfWork.Restaurants.Update(restaurant);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<RestaurantDto>(restaurant);
        }

        public async Task<byte[]> GetRestaurantQrImageBySlugAsync(string slug)
        {
            var restaurant = await _unitOfWork.Restaurants.FirstOrDefaultAsync(r => r.Slug == slug);

            if (restaurant == null)
                throw new DomainException(QrMessage.QrError.NO_RESTAURANT_FOUND_TO_GENERATE_QR);

            string fullUrl = $"https://scan2order.id.vn/{slug}";

            return _qrCodeService.GenerateRestaurantQrCodeBytes(fullUrl);
        }
    }
}
