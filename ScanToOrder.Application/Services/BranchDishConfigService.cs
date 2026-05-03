using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class BranchDishConfigService : IBranchDishConfigService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDishRedisService _dishRedisService;
        private readonly IMenuCacheService _menuCacheService;
        private readonly IRestaurantMenuService _restaurantMenuService;

        public BranchDishConfigService(IUnitOfWork unitOfWork, IMapper mapper, IDishRedisService dishRedisService, IMenuCacheService menuCacheService, IRestaurantMenuService restaurantMenuService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dishRedisService = dishRedisService;
            _menuCacheService = menuCacheService;
            _restaurantMenuService = restaurantMenuService;
        }

        public async Task<BranchDishConfigDto> ConfigDishByRestaurant(CreateBranchDishConfig request)
        {
            var existingRestaurant = await _unitOfWork.Restaurants.GetByIdAsync(request.RestaurantId);
            if (existingRestaurant == null)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            var existingDish = await _unitOfWork.Dishes.GetByIdAsync(request.DishId);
            if (existingDish == null)
                throw new Exception(Message.DishMessage.DishError.DISH_NOT_FOUND);

            var configExists = await _unitOfWork.BranchDishConfigs.ExistsAsync(
                x => x.RestaurantId == request.RestaurantId && x.DishId == request.DishId);
            if (configExists)
                throw new DomainException(BranchDishMessage.BranchDishError.BRANCH_DISH_ALREADY_EXISTS);

            var branchDishConfig = _mapper.Map<BranchDishConfig>(request);

            await _unitOfWork.BranchDishConfigs.AddAsync(branchDishConfig);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<BranchDishConfigDto>(branchDishConfig);
        }


        public async Task<List<BranchDishConfigDto>> GetBranchDishByRestaurant(int restaurantId)
        {
            var existingRestaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);
            if (existingRestaurant == null)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);

            var branchDishConfigs = await _unitOfWork
                .BranchDishConfigs
                .GetByRestaurantIdWithIncludeAsync(restaurantId);

            return _mapper.Map<List<BranchDishConfigDto>>(branchDishConfigs);
        }

        public async Task<BranchDishConfigDto> ToggleSoldOutAsync(int branchDishConfigId, bool isSoldOut)
        {
            var branchDishConfig = await _unitOfWork.BranchDishConfigs
                .GetByIdWithIncludeAsync(branchDishConfigId);

            if (branchDishConfig == null)
                throw new Exception(Message.BranchDishMessage.BranchDishError.BRANCH_DISH_NOT_FOUND);

            branchDishConfig.IsSoldOut = isSoldOut;

            _unitOfWork.BranchDishConfigs.Update(branchDishConfig);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<BranchDishConfigDto>(branchDishConfig);
        }

        public async Task<string> UpdateIsSoldOutBranchDish(int restaurantId, int dishId, bool isSoldOut, int quantity)
        {
            var restaurantIsExist = await _unitOfWork.Restaurants.ExistsAsync(x => x.Id == restaurantId);
            if (!restaurantIsExist)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            var dishIdExists = await _unitOfWork.Dishes.ExistsAsync(x => x.Id == dishId);
            if (!dishIdExists)
                throw new Exception(Message.DishMessage.DishError.DISH_NOT_FOUND);

            var branchDishConfig =
                (await _unitOfWork.BranchDishConfigs.FirstOrDefaultAsync(x =>
                    x.RestaurantId == restaurantId && x.DishId == dishId))
                .OrThrow(Message.BranchDishMessage.BranchDishError.BRANCH_DISH_ALREADY_EXISTS);
            branchDishConfig.IsSoldOut = isSoldOut;
            branchDishConfig.DishAvailability = isSoldOut ? 0 : quantity;

            _unitOfWork.BranchDishConfigs.Update(branchDishConfig);
            await _unitOfWork.SaveAsync();
            await _menuCacheService.SetDishStockInCacheAsync(restaurantId, dishId, isSoldOut, quantity);
            return Message.BranchDishMessage.BranchDishSuccess.BRANCH_DISH_SOLD_OUT_UPDATED;
        }

        public async Task<string> UpdateIsSellingBranchDish(int restaurantId, int dishId, bool isSelling)
        {
            var restaurantIsExist = await _unitOfWork.Restaurants.ExistsAsync(x => x.Id == restaurantId);
            if (!restaurantIsExist)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            
            var dishIdExists = await _unitOfWork.Dishes.ExistsAsync(x => x.Id == dishId &&  x.IsDeleted == false);
            if (!dishIdExists)
                throw new Exception(Message.DishMessage.DishError.DISH_NOT_FOUND);

            var branchDishConfigExists = await _unitOfWork.BranchDishConfigs.ExistsAsync(x => x.RestaurantId == restaurantId && x.DishId == dishId);
            if (!branchDishConfigExists)
                throw new Exception(Message.BranchDishMessage.BranchDishError.BRANCH_DISH_NOT_FOUND);
            
            // Update the selling status in Redis cache
            await _dishRedisService.SetDishSellingStatusAsync(restaurantId, dishId, isSelling);
            
            return Message.BranchDishMessage.BranchDishSuccess.BRANCH_DISH_IS_SELLING_UPDATED;
        }

        public async Task<string> UpdateIsSellingBranchDishByCategory(int restaurantId, int categoryId, bool isSelling)
        {
            var restaurantIsExist = await _unitOfWork.Restaurants.ExistsAsync(x => x.Id == restaurantId);
            if (!restaurantIsExist)
                throw new Exception(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            
            var categoryExists = await _unitOfWork.Categories.ExistsAsync(x => x.Id == categoryId && x.IsDeleted == false);
            if (!categoryExists)
                throw new Exception(Message.CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);

            var dishes = await _unitOfWork.Dishes.FindAsync(x => x.CategoryId == categoryId && x.IsDeleted == false);
            var dishIds = dishes.Select(x => x.Id).ToList();

            if (!dishIds.Any())
                return "Danh mục chưa có món ăn nào.";

            var branchDishConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(x => x.RestaurantId == restaurantId && dishIds.Contains(x.DishId));
            
            foreach (var config in branchDishConfigs)
            {
                config.IsSelling = isSelling;
                _unitOfWork.BranchDishConfigs.Update(config);
                
                await _dishRedisService.SetDishSellingStatusAsync(restaurantId, config.DishId, isSelling);
            }
            
            await _unitOfWork.SaveAsync();
            
            return Message.BranchDishMessage.BranchDishSuccess.BRANCH_DISH_IS_SELLING_UPDATED;
        }

        public async Task<string> SyncDishesToBranchDishConfigAsync(Guid tenantId)
        {
            var categories = await _unitOfWork.Categories.FindAsync(c => c.TenantId == tenantId);
            var categoryIds = categories.Select(c => c.Id).ToList();

            if (!categoryIds.Any())
                return DishMessage.DishError.NO_CATEGORY;

            var dishes = await _unitOfWork.Dishes.FindAsync(d => categoryIds.Contains(d.CategoryId) && !d.IsDeleted);
            if (!dishes.Any())
                return DishMessage.DishError.NO_DISH;
            var restaurants = await _unitOfWork.Restaurants.FindAsync(r => r.TenantId == tenantId && !r.IsDeleted);
            if (!restaurants.Any())
                return DishMessage.DishError.NO_RESTAURANT;

            var restaurantIds = restaurants.Select(r => r.Id).ToList();
            var existingConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(c => restaurantIds.Contains(c.RestaurantId));

            var newConfigs = new List<BranchDishConfig>();

            foreach (var restaurant in restaurants)
            {
                foreach (var dish in dishes)
                {
                    bool configExists = existingConfigs.Any(c => c.RestaurantId == restaurant.Id && c.DishId == dish.Id);
                    if (!configExists)
                    {
                        var newConfig = new BranchDishConfig
                        {
                            RestaurantId = restaurant.Id,
                            DishId = dish.Id,
                            Price = dish.Price,
                            IsSelling = true,
                            DishAvailability = 1,
                            IsSoldOut = false
                        };
                        newConfigs.Add(newConfig);
                    }
                }
            }

            if (newConfigs.Any())
            {
                await _unitOfWork.BranchDishConfigs.AddRangeAsync(newConfigs);
                await _unitOfWork.SaveAsync();
                return string.Format(DishMessage.DishSuccess.DISH_SYNC_SUCCESS, newConfigs.Count);
            }

            return DishMessage.DishSuccess.DISH_ALREADY_SYNCED;
        }
    }
}