using AutoMapper;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class RestaurantService : IRestaurantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public RestaurantService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
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
    }
}
