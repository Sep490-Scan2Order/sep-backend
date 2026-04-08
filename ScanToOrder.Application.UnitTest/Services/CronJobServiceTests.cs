using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Dishes; 
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
    }
}