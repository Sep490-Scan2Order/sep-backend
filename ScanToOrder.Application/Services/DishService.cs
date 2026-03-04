using AutoMapper;
using FluentValidation;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class DishService : IDishService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStorageService _storageService;
        private readonly IBranchDishConfigService _branchDishConfigService;
        private readonly IValidator<UpdateDishRequest> _updateDishValidator;

        public DishService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IStorageService storageService,
            IBranchDishConfigService branchDishConfigService,
            IValidator<UpdateDishRequest> updateDishValidator)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _branchDishConfigService = branchDishConfigService;
            _updateDishValidator = updateDishValidator;
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
                    uploadImageUrl = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "dishes");
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
            var validationResult = await _updateDishValidator.ValidateAsync(dishDto);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

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

            string uploadImageUrl = existingDish.ImageUrl;

            if (dishDto.ImageUrl != null && dishDto.ImageUrl.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await dishDto.ImageUrl.CopyToAsync(ms);
                    var fileBytes = ms.ToArray();

                    string extension = Path.GetExtension(dishDto.ImageUrl.FileName);
                    string fileName = $"dish_{Guid.NewGuid()}{extension}";
                    uploadImageUrl = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "dishes");
                }
                catch (Exception ex)
                {
                    throw new DomainException($"Lỗi khi tải ảnh lên: {ex.Message}");
                }
            }

            if (dishDto.DishName != null)
            {
                existingDish.DishName = dishDto.DishName.Trim();
            }

            if (dishDto.Price.HasValue)
            {
                existingDish.Price = dishDto.Price.Value;
            }

            if (!string.IsNullOrWhiteSpace(dishDto.Description))
            {
                existingDish.Description = dishDto.Description;
            }

            existingDish.ImageUrl = uploadImageUrl;

            if (dishDto.DishAvailability.HasValue)
            {
                existingDish.DishAvailability = dishDto.DishAvailability.Value;
                existingDish.IsAvailable = dishDto.DishAvailability.Value > 0;
            }

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

        public async Task<int> ImportDishesFromExcelAsync(Guid tenantId, IFormFile file)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var restaurants = await _unitOfWork.Restaurants.GetByTenantIdAsync(tenantId);
            if (restaurants == null || restaurants.Count == 0)
            {
                throw new DomainException(RestaurantMessage.RestaurantError.NOT_FOUND_RESTAURANT_FOR_USER);
            }

            var categories = await _unitOfWork.Categories.GetAllCategoriesByTenant(tenantId);
            var categoryDict = categories.ToDictionary(c => c.CategoryName.Trim(), c => c.Id);

            var createdCount = 0;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var lastUsedRow = worksheet.LastRowUsed();
            if (lastUsedRow == null)
            {
                return 0;
            }

            var lastRow = lastUsedRow.RowNumber();

            for (var row = 2; row <= lastRow; row++)
            {
                var categoryName = worksheet.Cell(row, 1).GetString().Trim();
                var dishName = worksheet.Cell(row, 2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(categoryName) || string.IsNullOrWhiteSpace(dishName))
                {
                    continue;
                }

                var price = worksheet.Cell(row, 3).GetValue<decimal>();
                var description = worksheet.Cell(row, 4).GetString();

                if (!categoryDict.TryGetValue(categoryName, out var categoryId))
                {
                    var category = new Category
                    {
                        CategoryName = categoryName,
                        TenantId = tenantId,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Categories.AddAsync(category);
                    await _unitOfWork.SaveAsync();

                    categoryId = category.Id;
                    categoryDict[categoryName] = categoryId;
                }

                var dish = new Dish
                {
                    CategoryId = categoryId,
                    DishName = dishName,
                    Price = price,
                    Description = description,
                    ImageUrl = string.Empty,
                    DishAvailability = 1,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _unitOfWork.Dishes.AddAsync(dish);
                await _unitOfWork.SaveAsync();

                var branchConfigs = new List<BranchDishConfig>();

                foreach (var restaurant in restaurants)
                {
                    branchConfigs.Add(new BranchDishConfig
                    {
                        RestaurantId = restaurant.Id,
                        DishId = dish.Id,
                        Price = dish.Price,
                        IsSelling = true,
                        IsSoldOut = false
                    });
                }

                if (branchConfigs.Count > 0)
                {
                    await _unitOfWork.BranchDishConfigs.AddRangeAsync(branchConfigs);
                    await _unitOfWork.SaveAsync();
                }

                createdCount++;
            }

            return createdCount;
        }
    }
}