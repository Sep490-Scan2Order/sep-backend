using AutoMapper;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class MenuRestaurantService : IMenuRestaurantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public MenuRestaurantService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MenuRestaurantDto>> GetMenuByRestaurantIdAsync(int restaurntId)
        {
            var menuRestaurants = await _unitOfWork.MenuRestaurants.FindAsync(x => x.RestaurantId == restaurntId);
            return _mapper.Map<IEnumerable<MenuRestaurantDto>>(menuRestaurants);
        }
        public async Task<MenuRestaurantDto> ApplyRestaurantWithTemplateAsync(CreateMenuRestaurantRequestDto createMenuRestaurantRequestDto)
        {
            var menuTemplate = await _unitOfWork.MenuTemplates.GetByIdAsync(createMenuRestaurantRequestDto.TemplateId);
            if (menuTemplate == null)
            {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);
            }
            bool isExist = await CheckTemplateExistWithRestaurantsAsync(createMenuRestaurantRequestDto.TemplateId, createMenuRestaurantRequestDto.RestaurantId);
            if (isExist) {
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_EXIST_WITH_RESTAURANT);
            }
            var menuRestaurant = _mapper.Map<MenuRestaurant>(createMenuRestaurantRequestDto);
            await _unitOfWork.MenuRestaurants.AddAsync(menuRestaurant);
            await _unitOfWork.SaveAsync();
            return _mapper.Map<MenuRestaurantDto>(menuRestaurant);
        }

        private async Task<bool> CheckTemplateExistWithRestaurantsAsync(int templateId, int restaurantId)
        {
            var menuRestaurant = await _unitOfWork.MenuRestaurants.FindAsync(x => x.RestaurantId == restaurantId && x.MenuTemplateId == templateId);
            return menuRestaurant != null && menuRestaurant.Any();
        }
    }
}
