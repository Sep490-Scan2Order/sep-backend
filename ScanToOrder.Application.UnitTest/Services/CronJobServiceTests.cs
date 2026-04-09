using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Configuration;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class CronJobServiceTests
    {
        private readonly Mock<ILogger<CronJobService>> _mockLogger;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOrderService> _mockOrderService;
        private readonly Mock<IDishRedisService> _mockDishRedisService;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<ISubscriptionService> _mockSubscriptionService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly CronJobService _service;


        public CronJobServiceTests()
        {
            _mockLogger = new Mock<ILogger<CronJobService>>();
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockOrderService = new Mock<IOrderService>();
            _mockDishRedisService = new Mock<IDishRedisService>();
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockSubscriptionService = new Mock<ISubscriptionService>();
            _mockEmailService = new Mock<IEmailService>();

            _service = new CronJobService(
                _mockLogger.Object,
                _mockUnitOfWork.Object,
                _mockOrderService.Object,
                _mockDishRedisService.Object,
                _mockRealtimeService.Object,
                _mockSubscriptionService.Object,
                _mockEmailService.Object
            );
        }

        #region 1. CancelExpiredUnpaidOrdersAsync

        [Fact]
        public async Task CancelExpiredUnpaidOrdersAsync_ExecutesSuccessfully_DoesNotThrow()
        {
            // Arrange
            _mockOrderService.Setup(s => s.CancelExpiredUnpaidOrdersAsync())
                             .Returns(Task.CompletedTask);

            // Act
            await _service.CancelExpiredUnpaidOrdersAsync();

            // Assert
            _mockOrderService.Verify(s => s.CancelExpiredUnpaidOrdersAsync(), Times.Once);
        }

        [Fact]
        public async Task CancelExpiredUnpaidOrdersAsync_ThrowsException_CatchesAndLogsError()
        {
            // Arrange
            _mockOrderService.Setup(s => s.CancelExpiredUnpaidOrdersAsync())
                             .ThrowsAsync(new Exception("Database timeout"));

            // Act
            Func<Task> action = async () => await _service.CancelExpiredUnpaidOrdersAsync();

            // Assert
            await action.Should().NotThrowAsync();
        }

        #endregion

        #region 2. SyncBranchDishSellingStatusAsync

        [Fact]
        public async Task SyncSellingStatus_NoRestaurantsToSync_EndsSuccessfully()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedSellingStatusesAsync())
                                 .ReturnsAsync(new List<int>());

            // Act
            await _service.SyncBranchDishSellingStatusAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task SyncSellingStatus_NoDishStatuses_ContinuesLoop()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedSellingStatusesAsync())
                                 .ReturnsAsync(new List<int> { 1 });
            _mockDishRedisService.Setup(s => s.GetDishSellingStatusesAsync(1))
                                 .ReturnsAsync(new Dictionary<int, bool>());

            // Act
            await _service.SyncBranchDishSellingStatusAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task SyncSellingStatus_NoConfigsInDb_ContinuesLoop()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedSellingStatusesAsync())
                                 .ReturnsAsync(new List<int> { 1 });
            _mockDishRedisService.Setup(s => s.GetDishSellingStatusesAsync(1))
                                 .ReturnsAsync(new Dictionary<int, bool> { { 10, true } });

            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
                           .ReturnsAsync(new List<BranchDishConfig>());

            // Act
            await _service.SyncBranchDishSellingStatusAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Never);
        }

        [Fact]
        public async Task SyncSellingStatus_HasUpdates_TryGetValueBranches_SavesToDb()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedSellingStatusesAsync())
                                 .ReturnsAsync(new List<int> { 1 });

            var redisStatuses = new Dictionary<int, bool> { { 10, false } };
            _mockDishRedisService.Setup(s => s.GetDishSellingStatusesAsync(1)).ReturnsAsync(redisStatuses);

            var dbConfigs = new List<BranchDishConfig>
            {
                new BranchDishConfig { DishId = 10, IsSelling = true },
                new BranchDishConfig { DishId = 99, IsSelling = true }
            };

            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
                           .ReturnsAsync(dbConfigs);

            // Act
            await _service.SyncBranchDishSellingStatusAsync();

            // Assert
            dbConfigs.First(c => c.DishId == 10).IsSelling.Should().BeFalse();
            dbConfigs.First(c => c.DishId == 99).IsSelling.Should().BeTrue();

            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(dbConfigs), Times.Once);
            _mockDishRedisService.Verify(s => s.ClearSyncedSellingStatusesAsync(1), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task SyncSellingStatus_ThrowsException_CatchesAndLogsError()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedSellingStatusesAsync())
                                 .ThrowsAsync(new Exception("Redis down"));

            // Act
            Func<Task> action = async () => await _service.SyncBranchDishSellingStatusAsync();

            // Assert
            await action.Should().NotThrowAsync();
        }

        #endregion

        #region 3. SyncBranchDishPriceAsync

        [Fact]
        public async Task SyncPrice_NoRestaurantsToSync_EndsSuccessfully()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedPricesAsync())
                                 .ReturnsAsync(new List<int>());

            // Act
            await _service.SyncBranchDishPriceAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task SyncPrice_NoDishPrices_ContinuesLoop()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedPricesAsync())
                                 .ReturnsAsync(new List<int> { 1 });
            _mockDishRedisService.Setup(s => s.GetDishPricesAsync(1))
                                 .ReturnsAsync(new Dictionary<int, decimal>());

            // Act
            await _service.SyncBranchDishPriceAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task SyncPrice_NoConfigsInDb_ContinuesLoop()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedPricesAsync())
                                 .ReturnsAsync(new List<int> { 1 });
            _mockDishRedisService.Setup(s => s.GetDishPricesAsync(1))
                                 .ReturnsAsync(new Dictionary<int, decimal> { { 10, 500m } });

            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
                           .ReturnsAsync(new List<BranchDishConfig>());

            // Act
            await _service.SyncBranchDishPriceAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Never);
        }

        [Fact]
        public async Task SyncPrice_HasUpdates_TryGetValueBranches_SavesToDb()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedPricesAsync())
                                 .ReturnsAsync(new List<int> { 1 });

            var redisPrices = new Dictionary<int, decimal> { { 10, 999m } };
            _mockDishRedisService.Setup(s => s.GetDishPricesAsync(1)).ReturnsAsync(redisPrices);

            var dbConfigs = new List<BranchDishConfig>
            {
                new BranchDishConfig { DishId = 10, Price = 100m },
                new BranchDishConfig { DishId = 99, Price = 200m }
            };

            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>()))
                           .ReturnsAsync(dbConfigs);

            // Act
            await _service.SyncBranchDishPriceAsync();

            // Assert
            dbConfigs.First(c => c.DishId == 10).Price.Should().Be(999m);
            dbConfigs.First(c => c.DishId == 99).Price.Should().Be(200m);

            _mockUnitOfWork.Verify(u => u.BranchDishConfigs.UpdateRange(dbConfigs), Times.Once);
            _mockDishRedisService.Verify(s => s.ClearSyncedPricesAsync(1), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task SyncPrice_ThrowsException_CatchesAndLogsError()
        {
            // Arrange
            _mockDishRedisService.Setup(s => s.GetAllRestaurantsWithUnsyncedPricesAsync())
                                 .ThrowsAsync(new Exception("Redis down"));

            // Act
            Func<Task> action = async () => await _service.SyncBranchDishPriceAsync();

            // Assert
            await action.Should().NotThrowAsync();
        }

        #endregion
        #region 4. UpdateRestaurantOpeningStatusAsync

        [Fact]
        public async Task UpdateStatus_NoActiveRestaurants_NoChanges()
        {
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.UpdateRestaurantOpeningStatusAsync();

            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatus_RestaurantMissingTime_Skipped()
        {
            var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "test-slug", IsActive = true, OpenTime = null, CloseTime = null } };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatus_NormalHours_WithinRange_OpensRestaurant()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MaxValue,
                    IsOpened = false
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            restaurants[0].IsOpened.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateStatus_NormalHours_WithinRange_AlreadyOpen_NoChange()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MaxValue,
                    IsOpened = true
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateStatus_NormalHours_OutsideRange_ClosesRestaurant()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = new TimeOnly(3,0), CloseTime = new TimeOnly(3,1),
                    IsOpened = true, IsReceivingOrders = false
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            restaurants[0].IsOpened.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatus_NormalHours_OutsideRange_ClosesAndStopsReceiving()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = new TimeOnly(3,0), CloseTime = new TimeOnly(3,1),
                    IsOpened = true, IsReceivingOrders = true
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            restaurants[0].IsOpened.Should().BeFalse();
            restaurants[0].IsReceivingOrders.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged("1", false), Times.Once);
        }

        [Fact]
        public async Task UpdateStatus_OvernightHours_WithinRange_OpensRestaurant()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = TimeOnly.MinValue, CloseTime = TimeOnly.MinValue,
                    IsOpened = false
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            restaurants[0].IsOpened.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateStatus_OvernightHours_OutsideRange_ClosesRestaurant()
        {
            var restaurants = new List<Restaurant>
            {
                new Restaurant
                {
                    Id = 1, Slug = "test-slug", IsActive = true,
                    OpenTime = TimeOnly.MaxValue, CloseTime = TimeOnly.MinValue,
                    IsOpened = true
                }
            };
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.UpdateRestaurantOpeningStatusAsync();

            restaurants[0].IsOpened.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateStatus_ThrowsException_CatchesAndLogsError()
        {
            _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ThrowsAsync(new Exception("DB Error"));

            Func<Task> action = async () => await _service.UpdateRestaurantOpeningStatusAsync();

            await action.Should().NotThrowAsync();
        }
        #endregion

        #region 5. ProcessSubscriptionExpirationsAsync

        [Fact]
        public async Task ProcessSubscription_ExecutesSuccessfully_CallsServiceOnce()
        {
            _mockSubscriptionService.Setup(s => s.ProcessSubscriptionExpirationsAsync())
                                    .Returns(Task.CompletedTask);

            await _service.ProcessSubscriptionExpirationsAsync();

            _mockSubscriptionService.Verify(s => s.ProcessSubscriptionExpirationsAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessSubscription_ThrowsException_CatchesAndLogsError()
        {
            _mockSubscriptionService.Setup(s => s.ProcessSubscriptionExpirationsAsync())
                                    .ThrowsAsync(new Exception("Error"));

            Func<Task> action = async () => await _service.ProcessSubscriptionExpirationsAsync();

            await action.Should().NotThrowAsync();
        }

        #endregion
        #region 6. CalculateWeeklyCommissionFeeAsync

        [Fact]
        public async Task CalcCommission_NoUnscannedOrders_CommitsAndReturnsEarly()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);
            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ReturnsAsync(new List<Order>());

            await _service.CalculateWeeklyCommissionFeeAsync();

            mockTxn.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task CalcCommission_NoConfig_UsesDefaultRate3Percent_And_HasNoDebt_SetsDebtStartedAt()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var orders = new List<Order>
            {
                new Order { Id = Guid.NewGuid(), FinalAmount = 100000, Restaurant = new Restaurant { TenantId = Guid.Empty, Slug = "test-slug" } }
            };
            var tenants = new List<Tenant>
            {
                new Tenant { Id = Guid.Empty, TotalDebtAmount = 0, DebtStartedAt = null }
            };

            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ReturnsAsync(orders);
            _mockUnitOfWork.Setup(u => u.Configurations.GetAllAsync(null))
                           .ReturnsAsync(new List<Configurations>());
            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .ReturnsAsync(tenants);

            await _service.CalculateWeeklyCommissionFeeAsync();

            tenants[0].TotalDebtAmount.Should().Be(3000); // 3% of 100000
            tenants[0].DebtStartedAt.Should().NotBeNull();
            orders[0].IsScanned.Should().BeTrue();

            _mockUnitOfWork.Verify(u => u.Tenants.UpdateRange(It.IsAny<IEnumerable<Tenant>>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.Orders.UpdateRange(It.IsAny<IEnumerable<Order>>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            mockTxn.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CalcCommission_HasConfig_UsesConfigRate_And_AlreadyHasDebt_DebtStartedAtUnchanged()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var orders = new List<Order>
            {
                new Order { Id = Guid.NewGuid(), FinalAmount = 100000, Restaurant = new Restaurant { TenantId = Guid.Empty, Slug = "test-slug" } }
            };
            var existingDate = new DateTime(2023, 1, 1);
            var tenants = new List<Tenant>
            {
                new Tenant { Id = Guid.Empty, TotalDebtAmount = 5000, DebtStartedAt = existingDate }
            };
            var configs = new List<Configurations> { new Configurations { CommissionRate = 5 } };

            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ReturnsAsync(orders);
            _mockUnitOfWork.Setup(u => u.Configurations.GetAllAsync(null))
                           .ReturnsAsync(configs);
            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .ReturnsAsync(tenants);

            await _service.CalculateWeeklyCommissionFeeAsync();

            tenants[0].TotalDebtAmount.Should().Be(10000); // 5000 + 5% of 100000 = 10000
            tenants[0].DebtStartedAt.Should().Be(existingDate); // Unchanged

            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task CalcCommission_TenantNotFoundInMap_LogsWarningAndContinues()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var orders = new List<Order>
            {
                new Order { Id = Guid.NewGuid(), FinalAmount = 100000, Restaurant = new Restaurant { TenantId = Guid.NewGuid(), Slug = "test-slug" } }
            };
            // Return empty tenants to simulate not found
            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ReturnsAsync(orders);
            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .ReturnsAsync(new List<Tenant>());

            await _service.CalculateWeeklyCommissionFeeAsync();

            _mockUnitOfWork.Verify(u => u.Tenants.UpdateRange(It.IsAny<IEnumerable<Tenant>>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Orders.UpdateRange(It.IsAny<IEnumerable<Order>>()), Times.Once); // Orders still marked scanned
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task CalcCommission_TotalFeeIsZero_SkipsTenant()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var orders = new List<Order>
            {
                new Order { Id = Guid.NewGuid(), FinalAmount = 0, Restaurant = new Restaurant { TenantId = Guid.Empty, Slug = "test-slug" } }
            };
            var tenants = new List<Tenant> { new Tenant { Id = Guid.Empty, TotalDebtAmount = 0 } };

            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ReturnsAsync(orders);
            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                           .ReturnsAsync(tenants);

            await _service.CalculateWeeklyCommissionFeeAsync();

            _mockUnitOfWork.Verify(u => u.Tenants.UpdateRange(It.IsAny<IEnumerable<Tenant>>()), Times.Never);
        }

        [Fact]
        public async Task CalcCommission_CancellationRequested_Rollbacks()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> action = async () => await _service.CalculateWeeklyCommissionFeeAsync(cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
            mockTxn.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CalcCommission_ThrowsGenericException_Rollbacks_DoesNotThrow()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            _mockUnitOfWork.Setup(u => u.Orders.GetAllAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<Expression<Func<Order, object>>>()))
                           .ThrowsAsync(new Exception("DB Error"));

            Func<Task> action = async () => await _service.CalculateWeeklyCommissionFeeAsync();

            await action.Should().NotThrowAsync();
            mockTxn.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region 7. MonitorAndSuspendOverdueDebtsAsync

        [Fact]
        public async Task MonitorDebts_NoTenantsWithDebt_CommitsAndReturnsEarly()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);
            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(new List<Tenant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            mockTxn.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue7Days_NotPreviouslySuspended_SendsSuspendEmail()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenantId = Guid.NewGuid();
            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = tenantId,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-8),
                    IsSuspended = false,
                    Account = new AuthenticationUser { Email = "test@example.com" }
                }
            };
            var restaurants = new List<Restaurant>
            {
                new Restaurant { Id = 1, TenantId = tenantId, IsActive = true, IsReceivingOrders = true, Slug = "test-slug" }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            tenants[0].IsSuspended.Should().BeTrue();
            restaurants[0].IsActive.Should().BeFalse();
            restaurants[0].IsReceivingOrders.Should().BeFalse();

            _mockEmailService.Verify(e => e.SendEmailAsync("test@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged("1", false), Times.Once);
            _mockRealtimeService.Verify(r => r.NotifyTenantProfileChanged(tenantId.ToString()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue7Days_AlreadySuspended_NoEmailSent()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = Guid.Empty,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-8),
                    IsSuspended = true,
                    Account = new AuthenticationUser { Email = "test@example.com" }
                }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue3Days_FirstWarning_SendsWarningEmail()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = Guid.Empty,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-4),
                    LastWarningSentAt = null,
                    Account = new AuthenticationUser { Email = "warn@example.com" }
                }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            tenants[0].LastWarningSentAt.Should().NotBeNull();
            _mockEmailService.Verify(e => e.SendEmailAsync("warn@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue3Days_WarningSentBefore_NoEmail()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = Guid.Empty,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-4),
                    LastWarningSentAt = DateTime.UtcNow.AddDays(-1),
                    Account = new AuthenticationUser { Email = "warn@example.com" }
                }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task MonitorDebts_TenantUnder3Days_NoAction()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = Guid.Empty,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-1),
                    IsSuspended = false
                }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            tenants[0].IsSuspended.Should().BeFalse();
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Tenants.UpdateRange(It.IsAny<IEnumerable<Tenant>>()), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_NoRestaurants_DoesNotCallRestaurantUpdateRange()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant { Id = Guid.Empty, DebtStartedAt = DateTime.UtcNow.AddDays(-1) }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>()); // Empty restaurants

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            _mockUnitOfWork.Verify(u => u.Restaurants.UpdateRange(It.IsAny<IEnumerable<Restaurant>>()), Times.Never);
        }

        [Fact]
        public async Task MonitorDebts_CancellationRequested_Rollbacks()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> action = async () => await _service.MonitorAndSuspendOverdueDebtsAsync(cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
            mockTxn.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_ThrowsGenericException_Rollbacks_DoesNotThrow()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ThrowsAsync(new Exception("DB Error"));

            Func<Task> action = async () => await _service.MonitorAndSuspendOverdueDebtsAsync();

            await action.Should().NotThrowAsync();
            mockTxn.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue7Days_NoEmailAccount_SuspendsButDoesNotSendEmail()
        {
            // Test cho đoạn màu vàng đầu tiên: Quá hạn 7 ngày nhưng Email trống
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenantId = Guid.NewGuid();
            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = tenantId,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-8),
                    IsSuspended = false,
                    // Giả lập Account không có email
                    Account = new AuthenticationUser { Email = string.Empty }
                }
            };
            var restaurants = new List<Restaurant>
            {
                new Restaurant { Id = 1, TenantId = tenantId, IsActive = true, IsReceivingOrders = true, Slug = "test-slug" }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            // Vẫn khóa tài khoản và nhà hàng
            tenants[0].IsSuspended.Should().BeTrue();
            restaurants[0].IsActive.Should().BeFalse();

            // NHƯNG không gọi hàm gửi mail
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue3Days_NoEmailAccount_UpdatesWarningDateButDoesNotSendEmail()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = Guid.Empty,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-4),
                    LastWarningSentAt = null,
                    Account = null
                }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(new List<Restaurant>());

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            tenants[0].LastWarningSentAt.Should().NotBeNull();

            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task MonitorDebts_TenantOverdue7Days_AccountIsNull_SuspendsButDoesNotSendEmail()
        {
            var mockTxn = new Mock<IDbTransaction>();
            _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTxn.Object);

            var tenantId = Guid.NewGuid();
            var tenants = new List<Tenant>
            {
                new Tenant
                {
                    Id = tenantId,
                    DebtStartedAt = DateTime.UtcNow.AddDays(-8),
                    IsSuspended = false,
                    Account = null 
                }
            };
            var restaurants = new List<Restaurant>
            {
                new Restaurant { Id = 1, TenantId = tenantId, IsActive = true, IsReceivingOrders = true, Slug = "test-slug" }
            };

            _mockUnitOfWork.Setup(u => u.Tenants.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>>()))
                           .ReturnsAsync(tenants);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                           .ReturnsAsync(restaurants);

            await _service.MonitorAndSuspendOverdueDebtsAsync();

            tenants[0].IsSuspended.Should().BeTrue();
            restaurants[0].IsActive.Should().BeFalse();

            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }
        #endregion
    }
}