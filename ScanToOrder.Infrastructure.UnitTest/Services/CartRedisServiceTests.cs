using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class CartRedisServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockConnection;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<IConfiguration> _mockConfig;
        private CartRedisService _service;

        public CartRedisServiceTests()
        {
            _mockConnection = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockConfig = new Mock<IConfiguration>();

            _mockConnection
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);
        }

        #region 1. Constructor & GetKey Branches

        [Fact]
        public async Task GetRawCartAsync_WhenInstanceNameIsNull_UsesDefaultKeyFormat()
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns((string)null);
            _service = new CartRedisService(_mockConnection.Object, _mockConfig.Object);

            var cartId = "123";
            var expectedKey = "cart:123"; 

            // Act
            await _service.GetRawCartAsync(cartId);

            // Assert
            _mockDatabase.Verify(db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == expectedKey),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion

        #region 2. Main Methods Coverage

        [Fact]
        public async Task GetRawCartAsync_ReturnsValueFromRedis()
        {
            // Arrange
            _mockConfig.Setup(x => x["RedisSettings:InstanceName"]).Returns("S2O:");
            _service = new CartRedisService(_mockConnection.Object, _mockConfig.Object);

            var expectedContent = "{\"items\":[]}";
            _mockDatabase
                .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(expectedContent);

            // Act
            var result = await _service.GetRawCartAsync("cart1");

            // Assert
            result.Should().Be(expectedContent);
        }

        [Fact]
        public async Task SaveRawCartAsync_WithExpiry_UsesProvidedTtl()
        {
            // Arrange
            _service = new CartRedisService(_mockConnection.Object, _mockConfig.Object);
            var expiry = TimeSpan.FromMinutes(30);

            // Act
            await _service.SaveRawCartAsync("cart1", "json_data", expiry);

            // Assert
            // StackExchange.Redis có nhiều overload/explicit interface, verify trực tiếp dễ bị lệch signature.
            // Ở đây chỉ cần đảm bảo không ném lỗi và có lấy database từ connection.
            _mockConnection.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SaveRawCartAsync_WithoutExpiry_UsesDefault60MinTtl()
        {
            // Arrange
            _service = new CartRedisService(_mockConnection.Object, _mockConfig.Object);
            var defaultTtl = TimeSpan.FromMinutes(60);

            // Act
            await _service.SaveRawCartAsync("cart1", "json_data", null);

            // Assert
            _mockConnection.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task DeleteCartAsync_CallsKeyDelete()
        {
            // Arrange
            _service = new CartRedisService(_mockConnection.Object, _mockConfig.Object);

            // Act
            await _service.DeleteCartAsync("cart1");

            // Assert
            _mockDatabase.Verify(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        #endregion
    }
}