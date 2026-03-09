using AutoMapper;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Exceptions;
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

        public async Task<MenuRestaurantDto> GetMenuByRestaurantIdAsync(int restaurantId)
        {
            var menuRestaurant = await _unitOfWork.MenuRestaurants.GetByFieldsIncludeAsync(
                x => x.RestaurantId == restaurantId && !x.IsDeleted,
                x => x.Restaurant,
                x => x.MenuTemplate    
            );

            if (menuRestaurant == null)
            {
                throw new DomainException(MenuTemplateMessage.MenuTemplateError.MENU_RESTAURANT_NOT_FOUND);
            }

            return _mapper.Map<MenuRestaurantDto>(menuRestaurant);
        }
        public async Task<MenuRestaurantDto> ApplyRestaurantWithTemplateAsync(CreateMenuRestaurantRequestDto request)
        {
            var menuTemplate = await _unitOfWork.MenuTemplates.GetByIdAsync(request.TemplateId);
            if (menuTemplate == null)
                throw new Exception(MenuTemplateMessage.MenuTemplateError.TEMPLATE_NOT_FOUND);

            MenuRestaurant menuRestaurant;

            var existingMenuRestaurant = await _unitOfWork.MenuRestaurants
                .FirstOrDefaultAsync(mr => mr.RestaurantId == request.RestaurantId);

            if (existingMenuRestaurant != null)
            {
                _mapper.Map(request, existingMenuRestaurant);
                _unitOfWork.MenuRestaurants.Update(existingMenuRestaurant);
                menuRestaurant = existingMenuRestaurant;
            }
            else
            {
                menuRestaurant = _mapper.Map<MenuRestaurant>(request);
                await _unitOfWork.MenuRestaurants.AddAsync(menuRestaurant);
            }

            await _unitOfWork.SaveAsync();

            return _mapper.Map<MenuRestaurantDto>(menuRestaurant);
        }

    }
}
