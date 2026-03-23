using AutoMapper;
using FluentValidation;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Enums;
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
        private readonly IMenuCacheService _menuCacheService;
        private readonly IValidator<UpdateDishRequest> _updateDishValidator;
        private readonly IDishRedisService _dishRedisService;
        private readonly IBackgroundJobService _backgroundJobService;

        public DishService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IStorageService storageService,
            IBranchDishConfigService branchDishConfigService,
            IMenuCacheService menuCacheService,
            IValidator<UpdateDishRequest> updateDishValidator,
            IDishRedisService dishRedisService,
            IBackgroundJobService backgroundJobService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _storageService = storageService;
            _branchDishConfigService = branchDishConfigService;
            _menuCacheService = menuCacheService;
            _updateDishValidator = updateDishValidator;
            _dishRedisService = dishRedisService;
            _backgroundJobService = backgroundJobService;
        }


        // Crud Dish: Create, GetAllByTenant, Update, Delete (Soft Delete), DeActive, Active
        public async Task<DishDto> CreateDish(Guid tenantId, int categoryId, CreateDishRequest dishDto)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var existCategory =
                await _unitOfWork.Categories.GetByFieldsIncludeAsync(x => x.Id == categoryId && x.TenantId == tenantId);
            if (existCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

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
                    throw new DomainException(string.Format(DishMessage.DishError.IMAGE_UPLOAD_ERROR, ex.Message));
                }
            }

            var dishEntity = _mapper.Map<Dish>(dishDto);
            dishEntity.CategoryId = categoryId;
            dishEntity.DishName = dishDto.DishName;
            dishEntity.Price = dishDto.Price;
            dishEntity.Description = dishDto.Description;
            dishEntity.ImageUrl = uploadImageUrl;
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
            
            _backgroundJobService.EnqueueSearchIndexDish(dishEntity.Id);
            
            return _mapper.Map<DishDto>(dishEntity);
        }

        public async Task<DishDto> CreateCombo(Guid tenantId, int categoryId, CreateComboRequest request)
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

            if (request.Items == null || !request.Items.Any())
            {
                throw new DomainException("Combo phải có ít nhất 1 món ăn.");
            }

            var dishIds = request.Items.Select(i => i.DishId).Distinct().ToList();
            var dishes = await _unitOfWork.Dishes.FindAsync(d => dishIds.Contains(d.Id) && !d.IsDeleted);
            
            if (dishes == null || dishes.Count() != dishIds.Count)
            {
                throw new DomainException("Một hoặc nhiều món ăn không tồn tại.");
            }

            if (dishes.Any(d => d.Type != DishType.Single))
            {
                throw new DomainException("Combo chỉ được bao gồm các món ăn lẻ (Single).");
            }

            string uploadImageUrl = string.Empty;
            if (request.ImageUrl != null && request.ImageUrl.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await request.ImageUrl.CopyToAsync(ms);
                    var fileBytes = ms.ToArray();

                    string extension = Path.GetExtension(request.ImageUrl.FileName);
                    string fileName = $"combo_{Guid.NewGuid()}{extension}";
                    uploadImageUrl = await _storageService.UploadFromBytesAsync(fileBytes, fileName, "dishes");
                }
                catch (Exception ex)
                {
                    throw new DomainException($"Lỗi khi tải ảnh lên: {ex.Message}");
                }
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var comboEntity = new Dish
                {
                    CategoryId = categoryId,
                    DishName = request.ComboName,
                    Price = request.Price,
                    Description = request.Description ?? string.Empty,
                    ImageUrl = uploadImageUrl,
                    Type = DishType.Combo,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _unitOfWork.Dishes.AddAsync(comboEntity);
                await _unitOfWork.SaveAsync();

                var comboDetails = request.Items.Select(item => new ComboDetail
                {
                    DishId = comboEntity.Id,
                    ItemDishId = item.DishId,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1
                }).ToList();

                await _unitOfWork.ComboDetails.AddRangeAsync(comboDetails);

                var restaurantId = await _unitOfWork.Restaurants.GetByTenantIdAsync(tenantId);
                var branchConfigs = new List<BranchDishConfig>();

                foreach (var res in restaurantId)
                {
                    var config = new BranchDishConfig
                    {
                        RestaurantId = res.Id,
                        DishId = comboEntity.Id,
                        Price = comboEntity.Price,
                        IsSelling = true,
                        IsSoldOut = false
                    };
                    branchConfigs.Add(config);
                }

                await _unitOfWork.BranchDishConfigs.AddRangeAsync(branchConfigs);
                await _unitOfWork.SaveAsync();

                await transaction.CommitAsync();

                _backgroundJobService.EnqueueSearchIndexDish(comboEntity.Id);

                return _mapper.Map<DishDto>(comboEntity);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<DishDto>> GetAllDishesByTenant(Guid tenantId, bool includeDeleted = false)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var dishes = await _unitOfWork.Dishes.GetAllDishesByTenant(tenantId, includeDeleted);
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

            var existCategory =
                await _unitOfWork.Categories.GetByFieldsIncludeAsync(x => x.Id == categoryId && x.TenantId == tenantId);
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

            bool priceChanged = false;
            if (dishDto.Price.HasValue && existingDish.Price != dishDto.Price.Value)
            {
                existingDish.Price = dishDto.Price.Value;
                priceChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(dishDto.Description))
            {
                existingDish.Description = dishDto.Description;
            }

            existingDish.ImageUrl = uploadImageUrl;

            existingDish.CategoryId = categoryId;

            _unitOfWork.Dishes.Update(existingDish);

            if (priceChanged)
            {
                var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(c => c.DishId == dishId);
                var distinctRestaurants = branchConfigs.Select(c => c.RestaurantId).Distinct();
                foreach (var resId in distinctRestaurants)
                {
                    await _dishRedisService.SetDishPriceAsync(resId, existingDish.Id, existingDish.Price);
                }
            }

            await _unitOfWork.SaveAsync();

            _backgroundJobService.EnqueueSearchIndexDish(existingDish.Id);

            return _mapper.Map<DishDto>(existingDish);
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
            var categoryDict = categories.ToDictionary(
                c => c.CategoryName.Trim().ToLowerInvariant(),
                c => c.Id
            );

            var restaurantsIds = restaurants.Select(r => r.Id).ToList();

            var existingDishes = await _unitOfWork.Dishes.GetAllDishesByTenant(tenantId, includeDeleted: true);
            var dishDict = existingDishes
                .GroupBy(d => $"{d.CategoryId}:{d.DishName.Trim().ToLowerInvariant()}")
                .ToDictionary(g => g.Key, g => g.First());

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

            var rows = new List<(string categoryName, string dishName, decimal price, string description, bool isCombo, string comboItems)>();

            for (var row = 2; row <= lastRow; row++)
            {
                var categoryName = worksheet.Cell(row, 1).GetString().Trim();
                var dishName = worksheet.Cell(row, 2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(categoryName) || string.IsNullOrWhiteSpace(dishName))
                    continue;

                var price = worksheet.Cell(row, 3).GetValue<decimal>();
                var description = worksheet.Cell(row, 4).GetString();

                var dishTypeRaw = worksheet.Cell(row, 5).GetString()?.Trim();
                var isCombo = !string.IsNullOrWhiteSpace(dishTypeRaw)
                              && dishTypeRaw.Equals("Combo", StringComparison.OrdinalIgnoreCase);

                var comboItems = worksheet.Cell(row, 6).GetString()?.Trim() ?? string.Empty;

                rows.Add((categoryName, dishName, price, description, isCombo, comboItems));
            }

            static List<(string? categoryName, string dishName, int quantity)> ParseComboItems(
                string comboItems)
            {
                var result = new List<(string? categoryName, string dishName, int quantity)>();
                if (string.IsNullOrWhiteSpace(comboItems))
                    return result;

                var parts = comboItems.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var p = part.Trim();
                    if (string.IsNullOrWhiteSpace(p))
                        continue;

                    var qtySplit = p.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (qtySplit.Length != 2)
                        throw new DomainException($"ComboItems invalid format. Missing ':' in part: {p}");

                    var left = qtySplit[0].Trim();
                    var qtyStr = qtySplit[1].Trim();
                    if (!int.TryParse(qtyStr, out var qty) || qty <= 0)
                        throw new DomainException($"ComboItems invalid quantity. Part: {p}");

                    string? itemCategoryName = null;
                    string itemDishName;
                    var catDishSplit = left.Split('|', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (catDishSplit.Length == 2)
                    {
                        itemCategoryName = catDishSplit[0].Trim();
                        itemDishName = catDishSplit[1].Trim();
                    }
                    else
                    {
                        // Component viết dạng "DishName:Qty" thì categoryName = null (backend sẽ resolve theo dishName).
                        itemDishName = left;
                    }

                    if (string.IsNullOrWhiteSpace(itemDishName))
                        throw new DomainException($"ComboItems invalid item. Part: {p}");

                    result.Add((itemCategoryName, itemDishName, qty));
                }

                return result;
            }

            foreach (var r in rows.Where(x => !x.isCombo))
            {
                var normalizedCategoryName = r.categoryName.ToLowerInvariant();
                if (!categoryDict.TryGetValue(normalizedCategoryName, out var categoryId))
                {
                    var category = new Category
                    {
                        CategoryName = r.categoryName,
                        TenantId = tenantId,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Categories.AddAsync(category);
                    await _unitOfWork.SaveAsync();

                    categoryId = category.Id;
                    categoryDict[normalizedCategoryName] = categoryId;
                }

                var normalizedDishName = r.dishName.ToLowerInvariant();
                var dishKey = $"{categoryId}:{normalizedDishName}";

                if (!dishDict.TryGetValue(dishKey, out var dish))
                {
                    dish = new Dish
                    {
                        CategoryId = categoryId,
                        DishName = r.dishName,
                        Price = r.price,
                        Description = r.description,
                        ImageUrl = string.Empty,
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false,
                        Type = DishType.Single
                    };

                    await _unitOfWork.Dishes.AddAsync(dish);
                    await _unitOfWork.SaveAsync();

                    dishDict[dishKey] = dish;
                    createdCount++;
                    _backgroundJobService.EnqueueSearchIndexDish(dish.Id);
                }
                else
                {
                    bool updated =
                        dish.Price != r.price ||
                        dish.Description != r.description ||
                        !dish.IsAvailable ||
                        dish.IsDeleted ||
                        dish.Type != DishType.Single;

                    dish.DishName = r.dishName;
                    dish.Price = r.price;
                    dish.Description = r.description;
                    dish.IsAvailable = true;
                    dish.IsDeleted = false;
                    dish.Type = DishType.Single;

                    if (updated)
                    {
                        _unitOfWork.Dishes.Update(dish);
                        await _unitOfWork.SaveAsync();
                        _backgroundJobService.EnqueueSearchIndexDish(dish.Id);
                    }
                }

                var existingBranchConfigs = await _unitOfWork.BranchDishConfigs
                    .FindAsync(bdc => bdc.DishId == dish.Id && restaurantsIds.Contains(bdc.RestaurantId));

                var branchByRestaurantId = existingBranchConfigs
                    .GroupBy(b => b.RestaurantId)
                    .ToDictionary(g => g.Key, g => g.First());

                var branchConfigsToAdd = new List<BranchDishConfig>();
                foreach (var restaurant in restaurants)
                {
                    if (branchByRestaurantId.TryGetValue(restaurant.Id, out var existingConfig))
                    {
                        existingConfig.Price = dish.Price;
                        existingConfig.IsSelling = true;
                        existingConfig.IsSoldOut = false;
                        existingConfig.IsDeleted = false;
                        _unitOfWork.BranchDishConfigs.Update(existingConfig);
                    }
                    else
                    {
                        branchConfigsToAdd.Add(new BranchDishConfig
                        {
                            RestaurantId = restaurant.Id,
                            DishId = dish.Id,
                            Price = dish.Price,
                            IsSelling = true,
                            IsSoldOut = false,
                            IsDeleted = false
                        });
                    }
                }

                if (branchConfigsToAdd.Count > 0)
                    await _unitOfWork.BranchDishConfigs.AddRangeAsync(branchConfigsToAdd);

                await _unitOfWork.SaveAsync();
            }

            foreach (var r in rows.Where(x => x.isCombo))
            {
                var normalizedCategoryName = r.categoryName.ToLowerInvariant();
                if (!categoryDict.TryGetValue(normalizedCategoryName, out var categoryId))
                {
                    var category = new Category
                    {
                        CategoryName = r.categoryName,
                        TenantId = tenantId,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Categories.AddAsync(category);
                    await _unitOfWork.SaveAsync();

                    categoryId = category.Id;
                    categoryDict[normalizedCategoryName] = categoryId;
                }

                var normalizedDishName = r.dishName.ToLowerInvariant();
                var dishKey = $"{categoryId}:{normalizedDishName}";

                if (!dishDict.TryGetValue(dishKey, out var comboDish))
                {
                    comboDish = new Dish
                    {
                        CategoryId = categoryId,
                        DishName = r.dishName,
                        Price = r.price,
                        Description = r.description,
                        ImageUrl = string.Empty,
                        IsAvailable = true,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false,
                        Type = DishType.Combo
                    };

                    await _unitOfWork.Dishes.AddAsync(comboDish);
                    await _unitOfWork.SaveAsync();

                    dishDict[dishKey] = comboDish;
                    createdCount++;
                    _backgroundJobService.EnqueueSearchIndexDish(comboDish.Id);
                }
                else
                {
                    bool updated =
                        comboDish.Price != r.price ||
                        comboDish.Description != r.description ||
                        !comboDish.IsAvailable ||
                        comboDish.IsDeleted ||
                        comboDish.Type != DishType.Combo;

                    comboDish.DishName = r.dishName;
                    comboDish.Price = r.price;
                    comboDish.Description = r.description;
                    comboDish.IsAvailable = true;
                    comboDish.IsDeleted = false;
                    comboDish.Type = DishType.Combo;

                    if (updated)
                    {
                        _unitOfWork.Dishes.Update(comboDish);
                        await _unitOfWork.SaveAsync();
                        _backgroundJobService.EnqueueSearchIndexDish(comboDish.Id);
                    }
                }

                var parsedComponents = ParseComboItems(r.comboItems);
                if (parsedComponents.Count == 0)
                    throw new DomainException($"Combo '{r.dishName}' thiếu ComboItems (cột 6).");

                var componentDishIds = new List<(int dishId, int quantity)>();
                var dishNameToDishes = dishDict
                    .Values
                    .GroupBy(d => d.DishName.Trim().ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var item in parsedComponents)
                {
                    int componentDishId;

                    if (!string.IsNullOrWhiteSpace(item.categoryName))
                    {
                        var compCategoryKey = item.categoryName.ToLowerInvariant();
                        if (!categoryDict.TryGetValue(compCategoryKey, out var compCategoryId))
                        {
                            var category = new Category
                            {
                                CategoryName = item.categoryName,
                                TenantId = tenantId,
                                IsActive = true,
                                IsDeleted = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _unitOfWork.Categories.AddAsync(category);
                            await _unitOfWork.SaveAsync();

                            compCategoryId = category.Id;
                            categoryDict[compCategoryKey] = compCategoryId;
                        }

                        var compDishKey = $"{compCategoryId}:{item.dishName.Trim().ToLowerInvariant()}";
                        if (!dishDict.TryGetValue(compDishKey, out var componentDish))
                            throw new DomainException(
                                $"Combo '{r.dishName}' component '{item.dishName}' không tìm thấy. " +
                                "Hãy đảm bảo component dish được import như dòng Single trước.");

                        if (componentDish.Type != DishType.Single)
                            throw new DomainException(
                                $"Combo '{r.dishName}' chỉ được bao gồm Single dishes. '{item.dishName}' hiện là Combo.");

                        componentDishId = componentDish.Id;
                    }
                    else
                    {
                        var dishNameKey = item.dishName.Trim().ToLowerInvariant();
                        if (!dishNameToDishes.TryGetValue(dishNameKey, out var matches) || matches.Count == 0)
                            throw new DomainException(
                                $"Combo '{r.dishName}' component '{item.dishName}' không tìm thấy. " +
                                "Hãy đảm bảo component dish được import như dòng Single trước.");

                        if (matches.Count > 1)
                            throw new DomainException(
                                $"Combo '{r.dishName}' component '{item.dishName}' bị trùng nhiều dish trong các category. " +
                                "Vui lòng ghi thêm category theo format 'CategoryName|DishName:Qty' để phân biệt.");

                        componentDishId = matches[0].Id;
                    }

                    componentDishIds.Add((componentDishId, item.quantity));
                }

                var existingComboDetails = await _unitOfWork.ComboDetails.FindAsync(cd => cd.DishId == comboDish.Id);
                if (existingComboDetails.Any())
                {
                    _unitOfWork.ComboDetails.RemoveRange(existingComboDetails);
                    await _unitOfWork.SaveAsync();
                }

                var newComboDetails = componentDishIds.Select(ci => new ComboDetail
                {
                    DishId = comboDish.Id,
                    ItemDishId = ci.dishId,
                    Quantity = ci.quantity
                }).ToList();

                if (newComboDetails.Count > 0)
                {
                    await _unitOfWork.ComboDetails.AddRangeAsync(newComboDetails);
                    await _unitOfWork.SaveAsync();
                }

                var existingBranchConfigs = await _unitOfWork.BranchDishConfigs
                    .FindAsync(bdc => bdc.DishId == comboDish.Id && restaurantsIds.Contains(bdc.RestaurantId));

                var branchByRestaurantId = existingBranchConfigs
                    .GroupBy(b => b.RestaurantId)
                    .ToDictionary(g => g.Key, g => g.First());

                var branchConfigsToAdd = new List<BranchDishConfig>();
                foreach (var restaurant in restaurants)
                {
                    if (branchByRestaurantId.TryGetValue(restaurant.Id, out var existingConfig))
                    {
                        existingConfig.Price = comboDish.Price;
                        existingConfig.IsSelling = true;
                        existingConfig.IsSoldOut = false;
                        existingConfig.IsDeleted = false;
                        _unitOfWork.BranchDishConfigs.Update(existingConfig);
                    }
                    else
                    {
                        branchConfigsToAdd.Add(new BranchDishConfig
                        {
                            RestaurantId = restaurant.Id,
                            DishId = comboDish.Id,
                            Price = comboDish.Price,
                            IsSelling = true,
                            IsSoldOut = false,
                            IsDeleted = false
                        });
                    }
                }

                if (branchConfigsToAdd.Count > 0)
                    await _unitOfWork.BranchDishConfigs.AddRangeAsync(branchConfigsToAdd);

                await _unitOfWork.SaveAsync();
            }

            foreach (var restaurantId in restaurantsIds.Distinct())
                await _menuCacheService.InvalidateMenuAsync(restaurantId);

            return createdCount;
        }

        public async Task<bool> DeleteDish(Guid tenantId, int categoryId, int dishId)
        {
            // 1. Kiểm tra Dish có tồn tại và thuộc đúng Tenant & Category không
            var existingDish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(
                x => x.Id == dishId && x.CategoryId == categoryId,
                x => x.Category
            );

            if (existingDish == null || existingDish.Category.TenantId != tenantId)
            {
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
            }

            if (existingDish.IsDeleted)
            {
                return true; // Nếu đã xóa rồi thì bỏ qua
            }

            // 2. Cập nhật trạng thái Soft Delete cho Dish
            existingDish.IsDeleted = true;
            _unitOfWork.Dishes.Update(existingDish);

            // 3. Tìm và xóa hẳn (Hard Delete) các BranchDishConfig liên kết với Dish này
            var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(b => b.DishId == dishId);
            if (branchConfigs.Any())
            {
                _unitOfWork.BranchDishConfigs.RemoveRange(branchConfigs);
            }

            // 4. Lưu thay đổi
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<bool> DeActiveDish(Guid tenantId, int categoryId, int dishId)
        {
            // 1. Kiểm tra món ăn (Dish) có tồn tại và đúng Tenant, Category hay không
            var existingDish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(
                x => x.Id == dishId && x.CategoryId == categoryId,
                x => x.Category
            );

            if (existingDish == null || existingDish.Category.TenantId != tenantId)
            {
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
            }

            // 2. Cập nhật trạng thái của Dish thành ngưng hoạt động
            existingDish.IsAvailable = false;
            _unitOfWork.Dishes.Update(existingDish);

            // 3. Tìm các cấu hình của món ăn này ở các chi nhánh (BranchDishConfig)
            var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(b => b.DishId == dishId);

            // 4. Cập nhật IsSelling = false cho tất cả chi nhánh
            if (branchConfigs.Any())
            {
                foreach (var config in branchConfigs)
                {
                    config.IsSelling = false;
                }

                // Nếu bạn đã thêm hàm UpdateRange vào GenericRepository như bài trước, 
                // bạn có thể dùng dòng dưới đây thay cho vòng lặp foreach để tối ưu hiệu suất:
                _unitOfWork.BranchDishConfigs.UpdateRange(branchConfigs);
            }

            // 5. Lưu lại thay đổi vào database
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<bool> ActiveDish(Guid tenantId, int categoryId, int dishId)
        {
            var existingDish = await _unitOfWork.Dishes.GetByFieldsIncludeAsync(
                x => x.Id == dishId && x.CategoryId == categoryId,
                x => x.Category
            );

            if (existingDish == null || existingDish.Category.TenantId != tenantId)
            {
                throw new DomainException(DishMessage.DishError.DISH_NOT_FOUND);
            }

            // 1. Mở lại trạng thái món ăn
            existingDish.IsAvailable = true;
            _unitOfWork.Dishes.Update(existingDish);

            // 2. Tìm và mở bán lại món này ở các chi nhánh
            var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(b => b.DishId == dishId);
            if (branchConfigs.Any())
            {
                foreach (var config in branchConfigs)
                {
                    config.IsSelling = true;
                }

                // Cập nhật hàng loạt nếu có hàm UpdateRange
                _unitOfWork.BranchDishConfigs.UpdateRange(branchConfigs);
            }

            await _unitOfWork.SaveAsync();
            return true;
        }
        
        public async Task<List<ComboDetailResponse>> GetComboById(int dishId)
        {
            var comboDetails = await _unitOfWork.ComboDetails.GetAllAsync(x => x.DishId.Equals(dishId),y => y.ItemDish, y => y.ItemDish.Category);
            if (comboDetails == null || !comboDetails.Any())
            {
                throw new DomainException(DishMessage.DishError.DISH_COMBO_NOT_FOUND);
            }
            
            var result = comboDetails.Select(d => new ComboDetailResponse
            {
                Dish = _mapper.Map<DishDto>(d.ItemDish),
                Quantity = d.Quantity
            }).ToList();
            
            return result;
        }
    }
}