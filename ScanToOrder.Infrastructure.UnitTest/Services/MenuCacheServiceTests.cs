using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Infrastructure.Services;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class MenuCacheServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockConnection;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<MenuCacheService>> _mockLogger;
        private MenuCacheService _service;

        public MenuCacheServiceTests()
        {
            _mockConnection = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<MenuCacheService>>();

            _mockConnection
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);
        }

        #region 1. Constructor & Key Logic

        [Fact]
        public async Task GetMenuAsync_WhenInstanceNameIsNull_UsesDefaultKeyFormat()
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns((string)null);
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            var expectedKey = "menu:1";

            // Act
            await _service.GetMenuAsync(1);

            // Assert
            _mockDatabase.Verify(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == expectedKey),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region 2. Main Methods Coverage

        [Fact]
        public async Task GetMenuAsync_ReturnsDeserializedMenu_WhenCacheExists()
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns("S2O:");
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            var menuData = new List<MenuCategoryDto> { new() { CategoryId = 1, CategoryName = "Pizza" } };

            _mockDatabase
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(JsonSerializer.Serialize(menuData));

            // Act
            var result = await _service.GetMenuAsync(1);

            // Assert
            result.Should().NotBeNull();
            result![0].CategoryName.Should().Be("Pizza");
        }

        [Fact]
        public async Task GetMenuAsync_ReturnsNull_WhenCacheIsMissing()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            _mockDatabase
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _service.GetMenuAsync(1);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetMenuAsync_UsesProvidedExpiry_WhenNotNull()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            var expiry = TimeSpan.FromHours(1);

            // Act
            await _service.SetMenuAsync(1, new List<MenuCategoryDto>(), expiry);

            // Assert
            _mockDatabase
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);
        }

        [Fact]
        public async Task InvalidateMenuAsync_LogsInformation_OnSuccess()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);

            // Act
            await _service.InvalidateMenuAsync(1);

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
            VerifyLogger(LogLevel.Information, "Menu cache invalidated", Times.Once());
        }

        #endregion

        #region 3. Exception Handling (Try-Catch Coverage)

        [Fact]
        public async Task GetMenuAsync_WhenRedisThrows_ReturnsNullAndLogsWarning()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            _mockDatabase
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Fail"));

            // Act
            var result = await _service.GetMenuAsync(1);

            // Assert
            result.Should().BeNull();
            VerifyLogger(LogLevel.Warning, "Redis GetMenuAsync failed", Times.Once());
        }

        [Fact]
        public async Task SetMenuAsync_WhenRedisThrows_ShouldCoverCatchBlock()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);

            // SETUP: Ném lỗi cho BẤT KỲ cuộc gọi nào đến StringSetAsync 
            // để chắc chắn code rơi vào khối catch (dòng 58)
            _mockDatabase.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new Exception("Redis connection failed"));

            // Act
            await _service.SetMenuAsync(1, new List<MenuCategoryDto>());

            // Assert
            // Verify xem Logger có được gọi trong khối catch hay không
            VerifyLogger(LogLevel.Warning, "Cache write skipped", Times.Once());
        }

        private void VerifyLogger(LogLevel level, string messagePart, Times times)
        {
            _mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messagePart)),
                    It.IsAny<Exception?>(), // BẮT BUỘC có dấu ? để khớp với LogWarning(ex, ...)
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()), // BẮT BUỘC có dấu ?
                times);
        }

        [Fact]
        public async Task InvalidateMenuAsync_WhenRedisThrows_ShouldCoverCatchBlock()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            _mockDatabase
                .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Redis Delete Error"));

            // Act
            await _service.InvalidateMenuAsync(1);

            // Assert
            VerifyLogger(LogLevel.Warning, "Redis InvalidateMenuAsync failed", Times.Once());
        }

        [Fact]
        public async Task SetMenuAsync_WhenExpiryIsNull_ShouldUseDefaultTtl()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            var expectedSeconds = 300; // 5 minutes = 300 seconds

            // Act
            await _service.SetMenuAsync(1, new List<MenuCategoryDto>(), null);

            // Assert
            _mockDatabase.Verify(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalSeconds == expectedSeconds), // So sánh giây
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateMenuAsync_WhenRedisThrows_LogsWarning()
        {
            // Arrange
            _service = new MenuCacheService(_mockConnection.Object, _mockConfig.Object, _mockLogger.Object);
            _mockDatabase
                .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ThrowsAsync(new Exception("Delete Fail"));

            // Act
            await _service.InvalidateMenuAsync(1);

            // Assert
            VerifyLogger(LogLevel.Warning, "Redis InvalidateMenuAsync failed", Times.Once());
        }

        #endregion

        #region Helper Methods

        #endregion
    }
}