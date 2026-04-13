using FluentAssertions;
using Moq;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class SearchIndexServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOpenAiService> _mockOpenAiService;
        private readonly SearchIndexService _service;

        public SearchIndexServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockOpenAiService = new Mock<IOpenAiService>();
            _service = new SearchIndexService(_mockUnitOfWork.Object, _mockOpenAiService.Object);
        }

        #region 1. IndexDishAsync

        [Fact]
        public async Task IndexDishAsync_WhenDishNotFound_ReturnsEarly()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync((Dish)null);

            // Act
            await _service.IndexDishAsync(1);

            // Assert
            _mockOpenAiService.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IndexDishAsync_WhenSearchTextIsEmpty_ReturnsEarly()
        {
            // Arrange
            var dish = new Dish { DishName = "", Description = "", Category = null };
            _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dish);

            // Act
            await _service.IndexDishAsync(1);

            // Assert
            _mockOpenAiService.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IndexDishAsync_WhenValid_UpdatesSearchVector()
        {
            // Arrange
            var dish = new Dish { Id = 1, DishName = "Phở", Description = "Ngon" };
            var mockEmbeddings = new float[] { 0.1f, 0.2f };

            _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dish);

            _mockOpenAiService.Setup(s => s.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(mockEmbeddings);

            // Act
            await _service.IndexDishAsync(1);

            // Assert
            dish.SearchVector.Should().NotBeNull();
            _mockUnitOfWork.Verify(u => u.Dishes.Update(dish), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task IndexDishAsync_WhenDishHasCategory_CoversAllBranches()
        {
            // Arrange
            var category = new Category { Id = 100, CategoryName = "Món chính" };
            var dish = new Dish
            {
                Id = 1,
                DishName = "Cơm Gà",
                Description = "Ngon bổ rẻ",
                Category = category 
            };
            var mockEmbeddings = new float[] { 0.1f, 0.5f, 0.9f };

            _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dish);

            _mockOpenAiService.Setup(s => s.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(mockEmbeddings);

            // Act
            await _service.IndexDishAsync(1);

            // Assert
            // Kiểm tra searchText có chứa tên Category không (logic ngầm)
            _mockOpenAiService.Verify(s => s.GetEmbeddingAsync(It.Is<string>(t => t.Contains("Món chính"))), Times.Once);
            dish.SearchVector.Should().NotBeNull();
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 2. IndexRestaurantAsync

        [Fact]
        public async Task IndexRestaurantAsync_WhenRestaurantNotFound_ReturnsEarly()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Restaurant)null);

            // Act
            await _service.IndexRestaurantAsync(1);

            // Assert
            _mockOpenAiService.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IndexRestaurantAsync_WhenValid_UpdatesRestaurantVector()
        {
            // Arrange
            var restaurant = new Restaurant
            {
                Id = 1,
                RestaurantName = "Quán A",
                Slug = "quan-a",
                TenantId = Guid.NewGuid()
            };
            var dishes = new List<Dish> { new Dish { DishName = "Cơm tấm" } };
            var mockEmbeddings = new float[] { 0.3f, 0.4f };

            _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
            _mockUnitOfWork.Setup(u => u.Dishes.GetAllAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dishes);

            _mockOpenAiService.Setup(s => s.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(mockEmbeddings);

            // Act
            await _service.IndexRestaurantAsync(1);

            // Assert
            restaurant.SearchVector.Should().NotBeNull();
            _mockUnitOfWork.Verify(u => u.Restaurants.Update(restaurant), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        #endregion

        #region 3. FullReIndexAsync

        [Fact]
        public async Task FullReIndexAsync_WhenCollectionsAreNull_DoesNotCrash()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Dishes.GetAllAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync((List<Dish>)null);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>(), It.IsAny<Expression<Func<Restaurant, object>>[]>()))
                .ReturnsAsync((List<Restaurant>)null);

            // Act
            await _service.FullReIndexAsync();

            // Assert
            _mockOpenAiService.Verify(s => s.GetEmbeddingAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task FullReIndexAsync_WhenCollectionsHaveData_CallsIndexingMethods()
        {
            // Arrange
            var dishes = new List<Dish> { new Dish { Id = 10, DishName = "A" } };
            var restaurants = new List<Restaurant>
            {
                new Restaurant { Id = 20, RestaurantName = "B", Slug = "restaurant-b" }
            };

            _mockUnitOfWork.Setup(u => u.Dishes.GetAllAsync(
                It.IsAny<Expression<Func<Dish, bool>>>(),
                It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dishes);

            _mockUnitOfWork.Setup(u => u.Restaurants.GetAllAsync(
                It.IsAny<Expression<Func<Restaurant, bool>>>(),
                It.IsAny<Expression<Func<Restaurant, object>>[]>()))
                .ReturnsAsync(restaurants);

            _mockUnitOfWork.Setup(u => u.Dishes.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Dish, bool>>>(), It.IsAny<Expression<Func<Dish, object>>[]>()))
                .ReturnsAsync(dishes[0]);
            _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(20))
                .ReturnsAsync(restaurants[0]);

            _mockOpenAiService.Setup(s => s.GetEmbeddingAsync(It.IsAny<string>()))
                .ReturnsAsync(new float[] { 0.1f });

            // Act
            await _service.FullReIndexAsync();

            // Assert
            _mockUnitOfWork.Verify(u => u.Dishes.Update(It.IsAny<Dish>()), Times.AtLeastOnce);
            _mockUnitOfWork.Verify(u => u.Restaurants.Update(It.IsAny<Restaurant>()), Times.AtLeastOnce);
        }

        #endregion
    }
}