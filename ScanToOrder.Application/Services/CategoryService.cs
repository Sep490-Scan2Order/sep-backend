using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CategoryService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<CategoryDto> CreateCategory(Guid tenantId, CreateCategoryRequest categoryDto)
        {
            var existingCategory =
                await _unitOfWork.Categories.GetByFieldsIncludeAsync(x =>
                    x.CategoryName.Equals(categoryDto.CategoryName));
            if (existingCategory != null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_ALREADY_EXISTS);
            }

            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var totalCategories = await _unitOfWork.Categories.GetTotalCategoriesByTenant(tenantId);

            var categoryEntity = _mapper.Map<Category>(categoryDto);
            categoryEntity.TenantId = tenantId;
            categoryEntity.CreatedAt = DateTime.UtcNow;
            categoryEntity.IsActive = true;
            categoryEntity.IsDeleted = false;

            await _unitOfWork.Categories.AddAsync(categoryEntity);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<CategoryDto>(categoryEntity);
        }

        public async Task<List<CategoryDto>> GetAllCategoriesByTenant(Guid tenantId)
        {
            var existTenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            var categories = await _unitOfWork.Categories.FindAsync(x => x.TenantId == tenantId && !x.IsDeleted);
            return _mapper.Map<List<CategoryDto>>(categories);
        }

        public async Task<CategoryDto> UpdateCategory(int categoryId, UpdateCategoryRequest categoryDto)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (existingCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

            existingCategory.CategoryName = categoryDto.CategoryName;

            _unitOfWork.Categories.Update(existingCategory);
            await _unitOfWork.SaveAsync();
            return _mapper.Map<CategoryDto>(existingCategory);
        }

        public async Task<bool> DeleteCategory(int categoryId)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (existingCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

            // 1. Cập nhật trạng thái Category
            existingCategory.IsDeleted = true;
            existingCategory.IsActive = false;
            _unitOfWork.Categories.Update(existingCategory);

            // 2. Tìm tất cả các món ăn (Dishes) thuộc Category này
            var dishes = await _unitOfWork.Dishes.FindAsync(d => d.CategoryId == categoryId);
            var dishIds = dishes.Select(d => d.Id).ToList();

            // 3. Nếu có món ăn, tìm và xóa các cấu hình chi nhánh (BranchDishConfig) liên quan
            if (dishIds.Any())
            {
                var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(b => dishIds.Contains(b.DishId));
                if (branchConfigs.Any())
                {
                    // Giả sử UnitOfWork/Repository của bạn có hàm RemoveRange. 
                    // Nếu không, bạn có thể lặp qua từng phần tử và gọi _unitOfWork.BranchDishConfigs.Remove(config)
                    _unitOfWork.BranchDishConfigs.RemoveRange(branchConfigs);
                }
            }

            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<bool> DeActiveCategory(int categoryId)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (existingCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

            // 1. Cập nhật trạng thái Category
            existingCategory.IsActive = false;
            _unitOfWork.Categories.Update(existingCategory);

            // 2. Tìm tất cả các món ăn (Dishes) thuộc Category này
            var dishes = await _unitOfWork.Dishes.FindAsync(d => d.CategoryId == categoryId);
            var dishIds = dishes.Select(d => d.Id).ToList();

            // 3. Nếu có món ăn, tìm và cập nhật IsSelling = false cho BranchDishConfig
            if (dishIds.Any())
            {
                var branchConfigs = await _unitOfWork.BranchDishConfigs.FindAsync(b => dishIds.Contains(b.DishId));
                foreach (var config in branchConfigs)
                {
                    config.IsSelling = false;
                    _unitOfWork.BranchDishConfigs.Update(config);
                }
            }

            await _unitOfWork.SaveAsync();
            return true;
        }

        public async Task<bool> ActiveCategory(int categoryId)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (existingCategory == null)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }

            // 1. Cập nhật trạng thái Category
            existingCategory.IsActive = true;
            _unitOfWork.Categories.Update(existingCategory);

            // 2. Tìm các món ăn (Dishes) thuộc Category này
            // LƯU Ý: Chỉ lấy những món chưa bị xóa và đang có trạng thái IsAvailable = true
            var dishes =
                await _unitOfWork.Dishes.FindAsync(d => d.CategoryId == categoryId && !d.IsDeleted && d.IsAvailable);
            var dishIds = dishes.Select(d => d.Id).ToList();

            // 3. Nếu có món ăn thỏa mãn, tìm và cập nhật IsSelling = true cho BranchDishConfig
            if (dishIds.Any())
            {
                var branchConfigs =
                    await _unitOfWork.BranchDishConfigs.FindAsync(b => dishIds.Contains(b.DishId)); // Hoặc b.DishId
                if (branchConfigs.Any())
                {
                    foreach (var config in branchConfigs)
                    {
                        config.IsSelling = true;
                    }

                    _unitOfWork.BranchDishConfigs.UpdateRange(branchConfigs);
                }
            }

            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}