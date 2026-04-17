using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScanToOrder.Infrastructure.Services;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class DishRedisServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockConnection;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<IConfiguration> _mockConfig;
        private DishRedisService _service;

        public DishRedisServiceTests()
        {
            _mockConnection = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockConfig = new Mock<IConfiguration>();

            _mockConnection
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);
        }

        #region 1. Constructor & Key Branches

        [Fact]
        public void Constructor_WhenConfigNull_UsesEmptyInstanceName()
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns((string)null);

            // Act
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);

            // Assert
            _service.Should().NotBeNull();
        }

        #endregion

        #region 2. Dish Selling Status Tests

        [Theory]
        [InlineData(true, "1")]
        [InlineData(false, "0")]
        public async Task SetDishSellingStatusAsync_CoversTernaryBranch(bool isSelling, string expectedValue)
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns("S2O:");
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);

            // Act
            await _service.SetDishSellingStatusAsync(1, 101, isSelling);

            // Assert
            _mockDatabase.Verify(db => db.HashSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains("BranchDishSelling:1")),
                "101",
                expectedValue,
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task GetDishSellingStatusesAsync_CoversTryParseBranches()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);
            var hashEntries = new HashEntry[]
            {
                new HashEntry("101", "1"),   
                new HashEntry("invalid", "1") 
            };
            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(hashEntries);

            // Act
            var result = await _service.GetDishSellingStatusesAsync(1);

            // Assert
            result.Should().HaveCount(1);
            result[101].Should().BeTrue();
        }

        [Fact]
        public async Task GetAllRestaurantsWithUnsyncedSellingStatusesAsync_CoversLoop()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);
            var members = new RedisValue[] { "1", "2", "invalid" };
            _mockDatabase.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(members);

            // Act
            var result = await _service.GetAllRestaurantsWithUnsyncedSellingStatusesAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(new[] { 1, 2 });
        }

        [Fact]
        public async Task ClearSyncedSellingStatusesAsync_CallsDeleteAndRemove()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);

            // Act
            await _service.ClearSyncedSellingStatusesAsync(1);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            _mockDatabase.Verify(db => db.SetRemoveAsync(It.IsAny<RedisKey>(), "1", It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region 3. Dish Price Tests

        [Fact]
        public async Task SetDishPriceAsync_UpdatesPrice()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);

            // Act
            await _service.SetDishPriceAsync(1, 202, 55000.5m);

            // Assert
            _mockDatabase.Verify(db => db.HashSetAsync(It.IsAny<RedisKey>(), "202", "55000.5", It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task GetDishPricesAsync_CoversMultiTryParseBranches()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);
            var hashEntries = new HashEntry[]
            {
                new HashEntry("202", "15000"),    
                new HashEntry("invalid", "15000"), 
                new HashEntry("203", "not-price")  
            };
            _mockDatabase.Setup(db => db.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(hashEntries);

            // Act
            var result = await _service.GetDishPricesAsync(1);

            // Assert
            result.Should().HaveCount(1);
            result[202].Should().Be(15000m);
        }

        [Fact]
        public async Task GetAllRestaurantsWithUnsyncedPricesAsync_CoversBranch()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);
            _mockDatabase.Setup(db => db.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue[] { "10" });

            // Act
            var result = await _service.GetAllRestaurantsWithUnsyncedPricesAsync();

            // Assert
            result.Should().Contain(10);
        }

        [Fact]
        public async Task ClearSyncedPricesAsync_CallsKeyDelete()
        {
            // Arrange
            _service = new DishRedisService(_mockConnection.Object, _mockConfig.Object);

            // Act
            await _service.ClearSyncedPricesAsync(5);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(It.Is<RedisKey>(k => k.ToString().Contains("BranchDishPrice:5")), It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion
    }
}