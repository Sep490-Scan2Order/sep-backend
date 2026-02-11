using AutoMapper;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Services.Def;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services.Impl
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
        public async Task<List<RestaurantDto>> GetAllRestaurantsAsync()
        {
            var restaurants = await _unitOfWork.Restaurants.GetAllAsync();

            var restaurantDtos = _mapper.Map<List<RestaurantDto>>(restaurants);

            return restaurantDtos;
        }
    }
}
