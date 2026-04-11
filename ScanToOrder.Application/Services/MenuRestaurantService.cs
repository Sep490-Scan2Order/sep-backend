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
        private readonly IPlanLimitationService _planLimitationService;

        public MenuRestaurantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPlanLimitationService planLimitationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _planLimitationService = planLimitationService;
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
            // Guard: check if the restaurant has the CanCustomMenuTemplate feature
            // If the plan doesn't support custom templates, only the default template (ID = 12) is allowed
            var features = await _planLimitationService.GetRestaurantFeaturesAsync(request.RestaurantId);
            if (!features.CanCustomMenuTemplate && request.TemplateId != 12)
                throw new DomainException(MenuTemplateMessage.MenuTemplateError.PLAN_FEATURE_CUSTOM_MENU_REQUIRED);

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
