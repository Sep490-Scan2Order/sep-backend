using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class PromotionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IDbTransaction> _mockTransaction;
        private readonly Mock<IPlanLimitationService> _mockPlanLimitation;
        private readonly PromotionService _service;

        public PromotionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _mockTransaction = new Mock<IDbTransaction>();
            _mockPlanLimitation = new Mock<IPlanLimitationService>();

            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync())
                .Returns(Task.FromResult(_mockTransaction.Object));


            _service = new PromotionService(_mockUnitOfWork.Object, _mockMapper.Object, _mockPlanLimitation.Object);
        }

        [Theory]
        [InlineData(PromotionType.Standard, null)]
        [InlineData(PromotionType.HappyHour, 10)]
        [InlineData(PromotionType.WeeklySpecial, 5)]
        [InlineData(PromotionType.Clearance, null)]
        public async Task CreatePromotionAsync_AllTypesAndPriority_ExecutesCorrectLogic(PromotionType type, int? priority)
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var dto = new CreatePromotionDto { Type = type, Priority = priority, IsGlobal = true };
            var promotion = new Promotion { Type = type };
            ApplyValidPromotionFieldsForType(promotion);

            _mockMapper.Setup(m => m.Map<Promotion>(dto)).Returns(promotion);

            // Act
            await _service.CreatePromotionAsync(tenantId, dto);

            // Assert
            promotion.TenantId.Should().Be(tenantId);
            _mockUnitOfWork.Verify(u => u.Promotions.AddAsync(promotion), Times.Once);
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreatePromotionAsync_NonGlobalDishScope_AddsMappings()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var dto = new CreatePromotionDto
            {
                IsGlobal = false,
                Scope = PromotionScope.Dish,
                DishIds = new List<int> { 1 },
                RestaurantIds = new List<int> { 101 }
            };
            var promotion = new Promotion { IsGlobal = false, Scope = PromotionScope.Dish };
            ApplyValidPromotionFieldsForType(promotion);

            _mockMapper.Setup(m => m.Map<Promotion>(dto)).Returns(promotion);

            // Act
            await _service.CreatePromotionAsync(tenantId, dto);

            // Assert
            _mockUnitOfWork.Verify(u => u.PromotionDishes.AddRangeAsync(It.IsAny<IEnumerable<PromotionDish>>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.RestaurantPromotions.AddRangeAsync(It.IsAny<IEnumerable<RestaurantPromotion>>()), Times.Once);
        }

        [Fact]
        public async Task CreatePromotionAsync_WhenException_RollsBack()
        {
            // Arrange
            var dto = new CreatePromotionDto();
            var promotion = new Promotion
            {
                Type = PromotionType.Standard,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1)
            };
            _mockMapper.Setup(m => m.Map<Promotion>(dto)).Returns(promotion);
            _mockUnitOfWork.Setup(u => u.SaveAsync()).ThrowsAsync(new Exception("db"));

            // Act
            Func<Task> action = async () => await _service.CreatePromotionAsync(Guid.NewGuid(), dto);

            // Assert
            await action.Should().ThrowAsync<Exception>();
            _mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPromotionByIdAsync_Exists_ReturnsDto()
        {
            // Arrange
            var promotion = new Promotion
            {
                Id = 1,
                IsDeleted = false,
                PromotionDishes = new List<PromotionDish> { new() { DishId = 10 } },
                RestaurantPromotions = new List<RestaurantPromotion> { new() { RestaurantId = 20 } }
            };

            _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(promotion);

            _mockMapper.Setup(m => m.Map<PromotionResponseDto>(promotion)).Returns(new PromotionResponseDto());

            // Act
            var result = await _service.GetPromotionByIdAsync(1);

            // Assert
            result.DishIds.Should().Contain(10);
            result.RestaurantIds.Should().Contain(20);
        }

        [Fact]
        public async Task GetPromotionByIdAsync_Deleted_ThrowsNotFoundException()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(new Promotion { IsDeleted = true });

            // Act
            Func<Task> action = async () => await _service.GetPromotionByIdAsync(1);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task UpdatePromotionAsync_Global_ClearsMappings()
        {
            // Arrange
            var existing = new Promotion
            {
                Id = 1,
                IsGlobal = false,
                Type = PromotionType.Standard,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1),
                PromotionDishes = new List<PromotionDish> { new() { DishId = 1 } },
                RestaurantPromotions = new List<RestaurantPromotion> { new() { RestaurantId = 1 } }
            };

            _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(existing);

            _mockMapper.Setup(m => m.Map(It.IsAny<UpdatePromotionDto>(), It.IsAny<Promotion>()))
                .Callback<UpdatePromotionDto, Promotion>((d, p) => p.IsGlobal = d.IsGlobal);

            // Act
            await _service.UpdatePromotionAsync(new UpdatePromotionDto { Id = 1, IsGlobal = true });

            // Assert
            existing.PromotionDishes.Should().BeEmpty();
            existing.RestaurantPromotions.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdatePromotionAsync_DishScope_UpdatesCollections()
        {
            // Arrange
            var existing = new Promotion
            {
                Id = 1,
                Type = PromotionType.HappyHour,
                IsGlobal = false,
                Scope = PromotionScope.Dish,
                DailyStartTime = TimeSpan.FromHours(16),
                DailyEndTime = TimeSpan.FromHours(18),
                PromotionDishes = new List<PromotionDish> { new() { DishId = 1 } },
                RestaurantPromotions = new List<RestaurantPromotion>()
            };

            _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(existing);

            // Act
            await _service.UpdatePromotionAsync(new UpdatePromotionDto { Id = 1, Scope = PromotionScope.Dish, DishIds = new List<int> { 2 }, RestaurantIds = new List<int> { 100 } });

            // Assert
            existing.PromotionDishes.Should().Contain(pd => pd.DishId == 2);
            existing.PromotionDishes.Should().NotContain(pd => pd.DishId == 1);
        }

        [Fact]
        public async Task DeletePromotionAsync_Exists_SoftDeletes()
        {
            // Arrange
            var promo = new Promotion { Id = 1, IsDeleted = false };
            _mockUnitOfWork.Setup(u => u.Promotions.GetByIdAsync(1)).ReturnsAsync(promo);

            // Act
            await _service.DeletePromotionAsync(1);

            // Assert
            promo.IsDeleted.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.Promotions.Update(promo), Times.Once);
        }

        [Fact]
        public async Task GetAvailablePromotionsByOrderAsync_CalculatesAndRanks()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var promos = new List<Promotion>
            {
                new() { Id = 1, Priority = 1, DiscountType = DiscountType.FixedAmount, DiscountValue = 5000, MinOrderValue = 0, IsActive = true, IsGlobal = true, RestaurantPromotions = new List<RestaurantPromotion>() },
                new() { Id = 2, Priority = 2, DiscountType = DiscountType.Percentage, DiscountValue = 10, MaxDiscountValue = 20000, MinOrderValue = 0, IsActive = true, IsGlobal = true, RestaurantPromotions = new List<RestaurantPromotion>() }
            };

            _mockUnitOfWork.Setup(u => u.Promotions.GetAllAsync(
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(promos);

            _mockPlanLimitation.Setup(p => p.GetRestaurantFeaturesAsync(It.IsAny<int>()))
                .ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });

            _mockMapper.Setup(m => m.Map<List<PromotionResponseDto>>(It.IsAny<List<Promotion>>()))
                .Returns(new List<PromotionResponseDto> { new() { Id = 1, Priority = 1 }, new() { Id = 2, Priority = 2 } });

            // Act
            var result = await _service.GetAvailablePromotionsByOrderAsync(tenantId, 1, 100000);

            // Assert
            result[0].Priority.Should().Be(2);
            result[0].IsRecommended.Should().BeTrue();
            result[0].DiscountAmount.Should().Be(10000);
        }

        [Fact]
        public async Task GetPromotionsByTenantAsync_ReturnsPagedData()
        {
            // Arrange
            var items = new List<Promotion>
            {
                new() { PromotionDishes = new List<PromotionDish>(), RestaurantPromotions = new List<RestaurantPromotion>() }
            };
            var paged = new PagedResult<Promotion>
            {
                Items = items,
                TotalCount = 1
            };

            _mockUnitOfWork.Setup(u => u.Promotions.GetPagedAndSortedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Promotion, bool>>>(),
                It.IsAny<Func<IQueryable<Promotion>, IOrderedQueryable<Promotion>>>(),
                It.IsAny<Expression<Func<Promotion, object>>[]>()))
                .ReturnsAsync(paged);

            _mockMapper.Setup(m => m.Map<List<PromotionResponseDto>>(It.IsAny<List<Promotion>>())).Returns(new List<PromotionResponseDto> { new() });

            // Act
            var result = await _service.GetPromotionsByTenantAsync(Guid.NewGuid());

            // Assert
            result.Items.Should().HaveCount(1);
        }

        private static void ApplyValidPromotionFieldsForType(Promotion promotion)
        {
            switch (promotion.Type)
            {
                case PromotionType.Standard:
                case PromotionType.Clearance:
                    promotion.StartDate ??= DateTime.UtcNow;
                    promotion.EndDate ??= DateTime.UtcNow.AddDays(7);
                    break;
                case PromotionType.HappyHour:
                    promotion.DailyStartTime ??= TimeSpan.FromHours(17);
                    promotion.DailyEndTime ??= TimeSpan.FromHours(19);
                    break;
                case PromotionType.WeeklySpecial:
                    promotion.StartDate ??= DateTime.UtcNow;
                    promotion.EndDate ??= DateTime.UtcNow.AddDays(7);
                    if (promotion.DaysOfWeek == DaysOfWeek.None)
                        promotion.DaysOfWeek = DaysOfWeek.Monday;
                    break;
            }
        }
    }
}