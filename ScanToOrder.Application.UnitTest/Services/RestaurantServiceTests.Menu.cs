using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Domain.Models;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Application.UnitTest.Services;

public partial class RestaurantServiceTests
{
    #region 9. GetRestaurantMenuAsync
    [Fact]
    public async Task GetRestaurantMenuAsync_WhenSellingOnlyAndCached_ReturnsCache()
    {
        var cachedMenu = new List<MenuCategoryDto> { new MenuCategoryDto { CategoryId = 1 } };
        _mockMenuCacheService.Setup(m => m.GetMenuAsync(1)).ReturnsAsync(cachedMenu);
        _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });

        var result = await _service.GetRestaurantMenuAsync(1, true);
        result.Should().BeEquivalentTo(cachedMenu);
        _mockUnitOfWork.Verify(u => u.Restaurants.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetRestaurantMenuAsync_RestaurantNotFound_ThrowsException()
    {
        _mockMenuCacheService.Setup(m => m.GetMenuAsync(1)).ReturnsAsync((List<MenuCategoryDto>?)null);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync((Restaurant?)null);
        _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });

        Func<Task> act = async () => await _service.GetRestaurantMenuAsync(1, true);
        await act.Should().ThrowAsync<DomainException>();
    }

        [Fact]
    public async Task GetRestaurantMenuAsync_BuildMenu_WeeklySpecialPromo_FallbackDiscountCapAndInvalidDiscountValue()
    {
        var tenantId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = 1, Slug = "slug", TenantId = tenantId };
        
        _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });
        _mockMenuCacheService.Setup(m => m.GetMenuAsync(1)).ReturnsAsync((List<MenuCategoryDto>?)null);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);

        var basePromos = new List<Promotion>
        {
            new Promotion
            {
                Id = 1, IsActive = true, IsDeleted = false, Scope = PromotionScope.Dish, IsGlobal = true,
                DiscountType = DiscountType.Percentage, DiscountValue = 50, MaxDiscountValue = 10000, 
                StartDate = ScanToOrder.Application.Utils.TimeUtils.GetVietnamTimeNow().AddDays(-1), 
                EndDate = ScanToOrder.Application.Utils.TimeUtils.GetVietnamTimeNow().AddHours(1), // Clamps trueExpiredAt
                Type = PromotionType.WeeklySpecial, DaysOfWeek = DaysOfWeek.All, DailyEndTime = null, 
                Priority = 10
            },
            new Promotion
            {
                Id = 2, IsActive = true, IsDeleted = false, Scope = PromotionScope.Dish, IsGlobal = true,
                DiscountType = DiscountType.FixedAmount, DiscountValue = 50000, 
                StartDate = DateTime.UtcNow.AddDays(-2), EndDate = DateTime.UtcNow.AddDays(-1), 
                Type = PromotionType.WeeklySpecial, DaysOfWeek = DaysOfWeek.All, DailyEndTime = new TimeSpan(23, 59, 0),
                Priority = 20
            },
            new Promotion // Tests MaxDiscountValue = null branch
            {
                Id = 3, IsActive = true, IsDeleted = false, Scope = PromotionScope.Dish, IsGlobal = true,
                DiscountType = DiscountType.FixedAmount, DiscountValue = 500, MaxDiscountValue = null, 
                StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(10), 
                Type = PromotionType.Standard, DaysOfWeek = DaysOfWeek.All, DailyEndTime = null,
                Priority = 5
            }
        };

        _mockUnitOfWork.Setup(u => u.Promotions.GetAllAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Promotion, bool>>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(basePromos);

        var branchDishes = new List<BranchDishConfig>
        {
            new BranchDishConfig
            {
                RestaurantId = 1, DishId = 1, Price = 30000, IsSelling = true, 
                Dish = new Dish { Category = new Category { Id = 1, CategoryName = "C1" } }
            },
            new BranchDishConfig
            {
                RestaurantId = 1, DishId = 2, Price = 3000, IsSelling = true, 
                Dish = new Dish { Category = new Category { Id = 1, CategoryName = "C1" } }
            }
        };
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetSellingDishesByRestaurantIdAsync(1)).ReturnsAsync(branchDishes);

        var result = await _service.GetRestaurantMenuAsync(1, true);

        var cat = result.First();
        var d1 = cat.Dishes.First(d => d.DishId == 1);
        d1.DiscountedPrice.Should().Be(20000);

        // Dish 2 (price=3000): Promo1 (50%, cap 10000) is valid => discount=1500 => result < original
        var d2 = cat.Dishes.First(d => d.DishId == 2);
        d2.DiscountedPrice.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task GetRestaurantMenuAsync_BuildMenu_AppliesPromoAndCache()
    {
        var tenantId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = 1, Slug = "slug", TenantId = tenantId };
        
        _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });
        _mockMenuCacheService.Setup(m => m.GetMenuAsync(1)).ReturnsAsync((List<MenuCategoryDto>?)null);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);

        var basePromos = new List<Promotion>
        {
            new Promotion
            {
                Id = 1, IsActive = true, IsDeleted = false, Scope = PromotionScope.Dish, IsGlobal = true,
                DiscountType = DiscountType.FixedAmount, DiscountValue = 20000, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
                Type = PromotionType.Standard, Priority = 10
            }
        };

        _mockUnitOfWork.Setup(u => u.Promotions.GetAllAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<Promotion, bool>>>(),
            It.IsAny<System.Linq.Expressions.Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(basePromos);

        var dishWithSpecificPromo = new Dish
        {
            Id = 1, DishName = "Dish 1", Price = 55000, Category = new Category { Id = 1, CategoryName = "Cat 1" }, Type = DishType.Single,
            PromotionDishes = new List<PromotionDish>
            {
                new PromotionDish
                {
                    Promotion = new Promotion
                    {
                        Id = 2, IsActive = true, IsDeleted = false, Scope = PromotionScope.Dish,
                        DiscountType = DiscountType.Percentage, DiscountValue = 50, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1), MaxDiscountValue = 15000,
                        Type = PromotionType.HappyHour, Priority = 20, DailyEndTime = new TimeSpan(23, 59, 59)
                    }
                }
            }
        };

        var comboDish = new Dish
        {
            Id = 2, DishName = "Combo 1", Price = 100000, Category = new Category { Id = 1, CategoryName = "Cat 1" }, Type = DishType.Combo,
            PromotionDishes = new List<PromotionDish>(),
            ComboDetails = new List<ComboDetail> { new ComboDetail { ItemDish = new Dish { DishName = "SubDish" }, Quantity = 1 } }
        };

        var branchDishes = new List<BranchDishConfig>
        {
            new BranchDishConfig { RestaurantId = 1, DishId = 1, Dish = dishWithSpecificPromo, Price = 55000, IsSelling = true },
            new BranchDishConfig { RestaurantId = 1, DishId = 2, Dish = comboDish, Price = 100000, IsSelling = true }
        };

        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetSellingDishesByRestaurantIdAsync(1)).ReturnsAsync(branchDishes);

        var result = await _service.GetRestaurantMenuAsync(1, true);

        var cat = result.First();
        var d1 = cat.Dishes.First(d => d.DishId == 1);
        d1.DiscountedPrice.Should().Be(40000);
        d1.PromotionLabel.Should().Be("-50%");
        
        var d2 = cat.Dishes.First(d => d.DishId == 2);
        d2.DiscountedPrice.Should().Be(80000);
        d2.PromotionLabel.Should().Be("-20k");
        
        _mockMenuCacheService.Verify(m => m.SetMenuAsync(1, result, It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task GetRestaurantMenuAsync_NotSellingOnly_SyncsWithRedis()
    {
        var tenantId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = 1, Slug = "slug", TenantId = tenantId };
        
        _mockPlanLimitationService.Setup(p => p.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = false });
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);

        var branchDishes = new List<BranchDishConfig>
        {
            new BranchDishConfig { RestaurantId = 1, DishId = 1, Dish = new Dish { Category = new Category { Id = 1, CategoryName = "Cat" } }, IsSelling = true }
        };
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetAllDishesByRestaurantIdAsync(1)).ReturnsAsync(branchDishes);

        var redisStatus = new Dictionary<int, bool> { { 1, false } };
        _mockDishRedisService.Setup(d => d.GetDishSellingStatusesAsync(1)).ReturnsAsync(redisStatus);

        var result = await _service.GetRestaurantMenuAsync(1, false);

        var dish = result.First().Dishes.First();
        dish.IsSelling.Should().BeFalse();
    }
    #endregion
}


