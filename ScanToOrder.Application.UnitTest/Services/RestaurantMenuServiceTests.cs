using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class RestaurantMenuServiceTests
    {
        private readonly Mock<IRestaurantService> _mockRestaurantService;
        private readonly RestaurantMenuService _service;

        public RestaurantMenuServiceTests()
        {
            _mockRestaurantService = new Mock<IRestaurantService>();
            _service = new RestaurantMenuService(_mockRestaurantService.Object);
        }

        [Fact]
        public async Task GetMenuForRestaurantAsync_CallsRestaurantService_ReturnsData()
        {
            // Arrange
            int restaurantId = 1;
            var expectedMenu = new List<MenuCategoryDto> { new MenuCategoryDto() };

            _mockRestaurantService.Setup(s => s.GetRestaurantMenuAsync(restaurantId, It.IsAny<bool>()))
                .ReturnsAsync(expectedMenu);

            // Act
            var result = await _service.GetMenuForRestaurantAsync(restaurantId);

            // Assert
            result.Should().BeEquivalentTo(expectedMenu);
            _mockRestaurantService.Verify(s => s.GetRestaurantMenuAsync(restaurantId, It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task GetAllMenuForRestaurantAsync_CallsRestaurantServiceWithFalse_ReturnsData()
        {
            // Arrange
            int restaurantId = 1;
            var expectedMenu = new List<MenuCategoryDto> { new MenuCategoryDto() };

            _mockRestaurantService.Setup(s => s.GetRestaurantMenuAsync(restaurantId, false))
                .ReturnsAsync(expectedMenu);

            // Act
            var result = await _service.GetAllMenuForRestaurantAsync(restaurantId);

            // Assert
            result.Should().BeEquivalentTo(expectedMenu);
            _mockRestaurantService.Verify(s => s.GetRestaurantMenuAsync(restaurantId, false), Times.Once);
        }
    }
}