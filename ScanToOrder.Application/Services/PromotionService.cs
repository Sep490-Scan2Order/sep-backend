using AutoMapper;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public PromotionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task CreatePromotionAsync(Guid tenantId, CreatePromotionDto dto)
    {
        var promotion = _mapper.Map<Promotion>(dto);

        // Set TenantId and Priority
        promotion.TenantId = tenantId;
        if (dto.Priority.HasValue)
        {
            promotion.Priority = dto.Priority.Value;
        }
        else
        {
            promotion.SetDefaultPriority();
        }

        // Reset fields based on PromotionType to ensure data integrity
        switch (promotion.Type)
        {
            case PromotionType.Standard:
            case PromotionType.Clearance:
                promotion.DailyStartTime = null;
                promotion.DailyEndTime = null;
                promotion.DaysOfWeek = DaysOfWeek.None;
                break;

            case PromotionType.HappyHour:
                promotion.DaysOfWeek = DaysOfWeek.None;
                break;

            case PromotionType.WeeklySpecial:
                break;
        }

        promotion.Validate();

        // Transaction to ensure atomicity of promotion creation and related entities
        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _unitOfWork.Promotions.AddAsync(promotion);
            await _unitOfWork.SaveAsync();

            // If the promotion is not global, handle mappings based on PromotionScope
            if (!promotion.IsGlobal)
            {
                // CASE 1: Dish-level promotion - Mapping specific dishes
                if (promotion.Scope == PromotionScope.Dish)
                {
                    if (dto.DishIds != null && dto.DishIds.Any())
                    {
                        var promotionDishes = dto.DishIds.Distinct().Select(dishId => new PromotionDish
                        {
                            PromotionId = promotion.Id,
                            DishId = dishId
                        }).ToList();

                        await _unitOfWork.PromotionDishes.AddRangeAsync(promotionDishes);
                    }
                }

                // Note: If Scope is Order, we ignore DishIds as it applies to the whole bill
                // CASE 2: Apply to specific restaurants (Valid for both Dish and Order scopes)
                if (dto.RestaurantIds != null && dto.RestaurantIds.Any())
                {
                    var restaurantPromotions = dto.RestaurantIds.Distinct().Select(resId => new RestaurantPromotion
                    {
                        PromotionId = promotion.Id,
                        RestaurantId = resId
                    }).ToList();

                    await _unitOfWork.RestaurantPromotions.AddRangeAsync(restaurantPromotions);
                }
            }

            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PromotionResponseDto> GetPromotionByIdAsync(int id)
    {
        var promotion = await _unitOfWork.Promotions.GetByFieldsIncludeAsync(p => p.Id == id,
            p => p.PromotionDishes,
            p => p.RestaurantPromotions);

        if (promotion == null || promotion.IsDeleted)
            throw new NotFoundException("Promotion", id);

        var dto = _mapper.Map<PromotionResponseDto>(promotion);

        dto.DishIds = promotion.PromotionDishes.Select(pd => pd.DishId).ToList();
        dto.RestaurantIds = promotion.RestaurantPromotions.Select(rp => rp.RestaurantId).ToList();

        return dto;
    }
    
    public async Task<PromotionResponseDto> GetPromotionByTenantAsync(Guid tenantId)
    {
        var promotion = await _unitOfWork.Promotions.GetByFieldsIncludeAsync(p => p.TenantId == tenantId,
            p => p.PromotionDishes,
            p => p.RestaurantPromotions);

        if (promotion == null || promotion.IsDeleted)
            throw new NotFoundException("Promotion", tenantId);

        var dto = _mapper.Map<PromotionResponseDto>(promotion);

        dto.DishIds = promotion.PromotionDishes.Select(pd => pd.DishId).ToList();
        dto.RestaurantIds = promotion.RestaurantPromotions.Select(rp => rp.RestaurantId).ToList();

        return dto;
    }

    public async Task UpdatePromotionAsync(UpdatePromotionDto dto)
    {
        var promotion = await _unitOfWork.Promotions.GetByFieldsIncludeAsync(p => p.Id == dto.Id,
            p => p.PromotionDishes,
            p => p.RestaurantPromotions);

        if (promotion == null || promotion.IsDeleted)
            throw new NotFoundException("Promotion", dto.Id);

        _mapper.Map(dto, promotion);

        switch (promotion.Type)
        {
            case PromotionType.Standard:
            case PromotionType.Clearance:
                promotion.DailyStartTime = null;
                promotion.DailyEndTime = null;
                promotion.DaysOfWeek = DaysOfWeek.None;
                break;
            case PromotionType.HappyHour:
                promotion.DaysOfWeek = DaysOfWeek.None;
                break;
        }

        if (!dto.Priority.HasValue) promotion.SetDefaultPriority();
        promotion.Validate();

        await using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (!promotion.IsGlobal)
            {
                if (promotion.Scope == PromotionScope.Dish)
                {
                    UpdatePromotionDishes(promotion, dto.DishIds ?? new List<int>());
                }
                else
                {
                    promotion.PromotionDishes.Clear();
                }
                UpdateRestaurantPromotions(promotion, dto.RestaurantIds ?? new List<int>());
            }
            else
            {
                promotion.PromotionDishes.Clear();
                promotion.RestaurantPromotions.Clear();
            }

            _unitOfWork.Promotions.Update(promotion);
            await _unitOfWork.SaveAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeletePromotionAsync(int id)
    {
        var promotion = await _unitOfWork.Promotions.GetByIdAsync(id);
        if (promotion == null)
            throw new NotFoundException("Promotion", id);

        promotion.IsDeleted = true; // Soft delete
        _unitOfWork.Promotions.Update(promotion);
        await _unitOfWork.SaveAsync();
    }

    
    // Helper methods to manage many-to-many relationships for PromotionDishes and RestaurantPromotions
    private void UpdatePromotionDishes(Promotion promotion, List<int> newDishIds)
    {
        // Remove dishes no longer in the list
        var toRemove = promotion.PromotionDishes
            .Where(pd => !newDishIds.Contains(pd.DishId)).ToList();
        foreach (var item in toRemove) promotion.PromotionDishes.Remove(item);

        // Add new dishes
        var existingIds = promotion.PromotionDishes.Select(pd => pd.DishId).ToList();
        var toAdd = newDishIds.Except(existingIds).Select(dishId => new PromotionDish
        {
            PromotionId = promotion.Id,
            DishId = dishId
        });

        foreach (var item in toAdd) promotion.PromotionDishes.Add(item);
    }

    private void UpdateRestaurantPromotions(Promotion promotion, List<int> newResIds)
    {
        var toRemove = promotion.RestaurantPromotions
            .Where(rp => !newResIds.Contains(rp.RestaurantId)).ToList();
        foreach (var item in toRemove) promotion.RestaurantPromotions.Remove(item);

        var existingIds = promotion.RestaurantPromotions.Select(rp => rp.RestaurantId).ToList();
        var toAdd = newResIds.Except(existingIds).Select(resId => new RestaurantPromotion
        {
            PromotionId = promotion.Id,
            RestaurantId = resId
        });

        foreach (var item in toAdd) promotion.RestaurantPromotions.Add(item);
    }
}