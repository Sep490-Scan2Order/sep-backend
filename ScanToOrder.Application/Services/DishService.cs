using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Application.Services
{
    public class DishService : IDishService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly IBranchDishConfigService _branchDishConfigService;

        public DishService(IUnitOfWork unitOfWork, IMapper mapper, IStorageService storageService, IBranchDishConfigService branchDishConfigService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _branchDishConfigService = branchDishConfigService;
        }

        public async Task<DishDto> CreateDish(Guid tenantId, int categoryId, CreateDishRequest dishDto)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }
            var existCategory = await _unitOfWork.Categories.GetByFieldsIncludeAsync(x => x.Id == categoryId && x.TenantId == tenantId);
            if (existCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }
            var totalDishes = await _unitOfWork.Dishes.GetTotalDishesByTenant(tenantId);

            string uploadImageUrl = string.Empty;
            if (dishDto.ImageUrl != null && dishDto.ImageUrl.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await dishDto.ImageUrl.CopyToAsync(ms);
                    var fileBytes = ms.ToArray();

                    string extension = Path.GetExtension(dishDto.ImageUrl.FileName);
                    string fileName = $"dish_{Guid.NewGuid()}{extension}";
                    uploadImageUrl = await _storageService.UploadQrCodeFromBytesAsync(fileBytes, fileName, "dishes");
                }
                catch (Exception ex)
                {
                    throw new DomainException($"Lỗi khi tải ảnh lên: {ex.Message}");
                }
            }

                // Đang bỏ giới hạn số lượng món ăn, nếu muốn giới hạn thì bỏ comment đoạn code dưới và thêm trường TotalDishes vào Tenant
                //if (totalDishes >= existTenant.TotalDishes) 
                //{
                //    throw new DomainException(DishMessage.DishError.DISH_OUT_OF_LIMIT);
                //}

                var dishEntity = _mapper.Map<Dish>(dishDto);
            dishEntity.CategoryId = categoryId;
            dishEntity.DishName = dishDto.DishName;
            dishEntity.Price = dishDto.Price;
            dishEntity.Description = dishDto.Description;
            dishEntity.ImageUrl = uploadImageUrl;
            dishEntity.DishAvailability = dishDto.DishAvailability;
            dishEntity.IsAvailable = true;
            dishEntity.CreatedAt = DateTime.UtcNow;
            dishEntity.IsDeleted = false;

            await _unitOfWork.Dishes.AddAsync(dishEntity);
            await _unitOfWork.SaveAsync();

            var restaurantId = await _unitOfWork.Restaurants.GetByTenantIdAsync(tenantId);

            var branchConfigs = new List<BranchDishConfig>();

            foreach (var res in restaurantId)
            {
                var config = new BranchDishConfig
                {
                    RestaurantId = res.Id,
                    DishId = dishEntity.Id, 
                    Price = dishEntity.Price,
                    IsSelling = true,
                    IsSoldOut = false
                };
                branchConfigs.Add(config);
            }

            await _unitOfWork.BranchDishConfigs.AddRangeAsync(branchConfigs);

            await _unitOfWork.SaveAsync();

            return _mapper.Map<DishDto>(dishEntity);
        }

        public async Task<List<DishDto>> GetAllDishesByTenant(Guid tenantId)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var dishes = await _unitOfWork.Dishes.GetAllDishesByTenant(tenantId);
            return _mapper.Map<List<DishDto>>(dishes);
        }

        public async Task<DishDto> UpdateDish(Guid tenantId, int categoryId, int dishId, UpdateDishRequest dishDto)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }
            var existCategory = await _unitOfWork.Categories.GetByFieldsIncludeAsync(x => x.Id == categoryId && x.TenantId == tenantId);
            if (existCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

            var existingDish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(
                x => x.Id == dishId,
                x => x.Category
            );

            if (existingDish == null || existingDish.Category.TenantId != tenantId)
            {
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
            }

            existingDish.DishName = dishDto.DishName;
            existingDish.Price = dishDto.Price;
            existingDish.Description = dishDto.Description;
            existingDish.ImageUrl = dishDto.ImageUrl;
            existingDish.DishAvailability = dishDto.DishAvailability;
            existingDish.IsAvailable = dishDto.DishAvailability > 0;
            existingDish.CategoryId = categoryId;

            _unitOfWork.Dishes.Update(existingDish);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<DishDto>(existingDish);
        }

        public async Task<bool> UpdateDishAvailability(Guid tenantId, int dishId, int dishAvailability)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }


            var existingDish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(
                x => x.Id == dishId,
                x => x.Category 
            );

            if (existingDish == null || existingDish.Category.TenantId != tenantId)
            {
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
            }

            if (dishAvailability < existingDish.DishAvailability)
            {
                throw new DomainException(DishMessage.DishError.INVALID_DISH_AVAILABILITY);
            }

            existingDish.DishAvailability = dishAvailability;

            _unitOfWork.Dishes.Update(existingDish);
            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}