using AutoMapper;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScanToOrder.Domain.Entities.Dishes;

namespace ScanToOrder.Application.Services
{
    public class DishService : IDishService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public DishService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
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
            if (totalDishes >= existTenant.TotalDishes) 
            {
                throw new DomainException(DishMessage.DishError.DISH_OUT_OF_LIMIT);
            }

            var dishEntity = _mapper.Map<Dish>(dishDto);
            dishEntity.CategoryId = categoryId;
            dishEntity.DishName = dishDto.DishName;
            dishEntity.Price = dishDto.Price;
            dishEntity.Description = dishDto.Description;
            dishEntity.ImageUrl = dishDto.ImageUrl;
            dishEntity.DishAvailability = dishDto.DishAvailability;
            dishEntity.IsAvailable = true;
            dishEntity.CreatedAt = DateTime.UtcNow;
            dishEntity.IsDeleted = false;

            await _unitOfWork.Dishes.AddAsync(dishEntity);
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