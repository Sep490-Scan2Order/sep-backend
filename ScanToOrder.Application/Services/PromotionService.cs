using AutoMapper;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Enums;
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
}