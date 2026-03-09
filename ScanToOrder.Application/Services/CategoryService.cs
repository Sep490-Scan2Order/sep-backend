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
            var existingCategory = await _unitOfWork.Categories.GetByFieldsIncludeAsync(x => x.CategoryName.Equals(categoryDto.CategoryName));
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

        public async Task<CategoryDto> UpdateCategory(Guid tenantId, int categoryId, UpdateCategoryRequest categoryDto)
        {
            var existingCategory = await _unitOfWork.Categories.GetByIdAsync(categoryId);
            if (existingCategory == null || existingCategory.TenantId != tenantId)
            {
                throw new DomainException(CategoryMessage.CategoryError.CATEGORY_NOT_FOUND);
            }
            var existTenant = _unitOfWork.Tenants.GetByIdAsync(tenantId).Result;
            if (existTenant == null)
            {
                throw new DomainException(TenantMessage.TenantError.TENANT_NOT_FOUND);
            }

            existingCategory.CategoryName = categoryDto.CategoryName;

             _unitOfWork.Categories.Update(existingCategory);
            await _unitOfWork.SaveAsync();
            return _mapper.Map<CategoryDto>(existingCategory);
        }
    }
}
