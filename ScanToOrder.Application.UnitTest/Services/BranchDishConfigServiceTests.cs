using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Dishes;
using ScanToOrder.Application.Interfaces; 
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services;

public class BranchDishConfigServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IDishRedisService> _mockDishRedisService;
    private readonly BranchDishConfigService _service;

    public BranchDishConfigServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockDishRedisService = new Mock<IDishRedisService>();

        _service = new BranchDishConfigService(
            _mockUnitOfWork.Object,
            _mockMapper.Object,
            _mockDishRedisService.Object
        );
    }

    #region 1. ConfigDishByRestaurant
    [Fact]
    public async Task ConfigDishByRestaurant_WhenRestaurantNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);

        var action = async () => await _service.ConfigDishByRestaurant(new CreateBranchDishConfig { RestaurantId = 1 });

        await action.Should().ThrowAsync<Exception>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task ConfigDishByRestaurant_WhenDishNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Restaurant { Slug = "test-slug" });
        _mockUnitOfWork.Setup(u => u.Dishes.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Dish)null);

        var action = async () => await _service.ConfigDishByRestaurant(new CreateBranchDishConfig { RestaurantId = 1, DishId = 1 });

        await action.Should().ThrowAsync<Exception>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task ConfigDishByRestaurant_WhenConfigExists_ThrowsDomainException()
    {
        // ExistsAsync chỉ có 1 tham số
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Restaurant { Slug = "test-slug" });
        _mockUnitOfWork.Setup(u => u.Dishes.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Dish());
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ExistsAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(true);

        var action = async () => await _service.ConfigDishByRestaurant(new CreateBranchDishConfig { RestaurantId = 1, DishId = 1 });

        await action.Should().ThrowAsync<DomainException>().WithMessage(BranchDishMessage.BranchDishError.BRANCH_DISH_ALREADY_EXISTS);
    }

    [Fact]
    public async Task ConfigDishByRestaurant_WhenValid_AddsAndReturnsDto()
    {
        var request = new CreateBranchDishConfig { RestaurantId = 1, DishId = 1 };
        var branchDishConfig = new BranchDishConfig { RestaurantId = 1, DishId = 1 };
        var expectedDto = new BranchDishConfigDto { RestaurantName = "Res", DishName = "Dish", DishImageUrl = "url" };

        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Restaurant { Slug = "test-slug" });
        _mockUnitOfWork.Setup(u => u.Dishes.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Dish());
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ExistsAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(false);
        _mockMapper.Setup(m => m.Map<BranchDishConfig>(request)).Returns(branchDishConfig);
        _mockMapper.Setup(m => m.Map<BranchDishConfigDto>(branchDishConfig)).Returns(expectedDto);

        var result = await _service.ConfigDishByRestaurant(request);

        result.Should().BeEquivalentTo(expectedDto);
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddAsync(branchDishConfig), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 2. GetBranchDishByRestaurant
    [Fact]
    public async Task GetBranchDishByRestaurant_WhenRestaurantNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);

        var action = async () => await _service.GetBranchDishByRestaurant(1);

        await action.Should().ThrowAsync<Exception>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task GetBranchDishByRestaurant_WhenValid_ReturnsListDto()
    {
        var configs = new List<BranchDishConfig> { new BranchDishConfig() };
        var expectedDtos = new List<BranchDishConfigDto> { new BranchDishConfigDto { RestaurantName = "Res", DishName = "Dish", DishImageUrl = "url" } };

        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Restaurant { Slug = "test-slug" });
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetByRestaurantIdWithIncludeAsync(It.IsAny<int>())).ReturnsAsync(configs);
        _mockMapper.Setup(m => m.Map<List<BranchDishConfigDto>>(configs)).Returns(expectedDtos);

        var result = await _service.GetBranchDishByRestaurant(1);

        result.Should().BeEquivalentTo(expectedDtos);
    }
    #endregion

    #region 3. ToggleSoldOutAsync
    [Fact]
    public async Task ToggleSoldOutAsync_WhenConfigNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetByIdWithIncludeAsync(It.IsAny<int>())).ReturnsAsync((BranchDishConfig)null);

        var action = async () => await _service.ToggleSoldOutAsync(1, true);

        await action.Should().ThrowAsync<Exception>().WithMessage(BranchDishMessage.BranchDishError.BRANCH_DISH_NOT_FOUND);
    }

    [Fact]
    public async Task ToggleSoldOutAsync_WhenValid_UpdatesAndReturnsDto()
    {
        var config = new BranchDishConfig { Id = 1, IsSoldOut = false };
        var expectedDto = new BranchDishConfigDto { RestaurantName = "Res", DishName = "Dish", DishImageUrl = "url", Id = 1, IsSoldOut = true };

        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetByIdWithIncludeAsync(1)).ReturnsAsync(config);
        _mockMapper.Setup(m => m.Map<BranchDishConfigDto>(config)).Returns(expectedDto);

        var result = await _service.ToggleSoldOutAsync(1, true);

        result.Should().BeEquivalentTo(expectedDto);
        config.IsSoldOut.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.Update(config), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 4. UpdateIsSoldOutBranchDish
    [Fact]
    public async Task UpdateIsSoldOutBranchDish_WhenRestaurantNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(false);

        var action = async () => await _service.UpdateIsSoldOutBranchDish(1, 1, true, 10);

        await action.Should().ThrowAsync<Exception>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateIsSoldOutBranchDish_WhenDishNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(false);

        var action = async () => await _service.UpdateIsSoldOutBranchDish(1, 1, true, 10);

        await action.Should().ThrowAsync<Exception>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateIsSoldOutBranchDish_WhenConfigNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(true);
        
        // Vẫn giữ tham số tùy chọn ở FirstOrDefaultAsync nếu Repos của bạn có hỗ trợ
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>())).ReturnsAsync((BranchDishConfig)null);

        var action = async () => await _service.UpdateIsSoldOutBranchDish(1, 1, true, 10);

        await action.Should().ThrowAsync<Exception>().WithMessage(BranchDishMessage.BranchDishError.BRANCH_DISH_ALREADY_EXISTS);
    }

    [Fact]
    public async Task UpdateIsSoldOutBranchDish_WhenIsSoldOutTrue_SetsAvailabilityToZero()
    {
        var config = new BranchDishConfig { RestaurantId = 1, DishId = 1, IsSoldOut = false, DishAvailability = 5 };
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>())).ReturnsAsync(config);

        var result = await _service.UpdateIsSoldOutBranchDish(1, 1, true, 10);

        result.Should().Be(BranchDishMessage.BranchDishSuccess.BRANCH_DISH_SOLD_OUT_UPDATED);
        config.IsSoldOut.Should().BeTrue();
        config.DishAvailability.Should().Be(0);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateIsSoldOutBranchDish_WhenIsSoldOutFalse_SetsAvailabilityToQuantity()
    {
        var config = new BranchDishConfig { RestaurantId = 1, DishId = 1, IsSoldOut = true, DishAvailability = 0 };
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>())).ReturnsAsync(config);

        var result = await _service.UpdateIsSoldOutBranchDish(1, 1, false, 15);

        result.Should().Be(BranchDishMessage.BranchDishSuccess.BRANCH_DISH_SOLD_OUT_UPDATED);
        config.IsSoldOut.Should().BeFalse();
        config.DishAvailability.Should().Be(15);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 5. UpdateIsSellingBranchDish
    [Fact]
    public async Task UpdateIsSellingBranchDish_WhenRestaurantNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(false);

        var action = async () => await _service.UpdateIsSellingBranchDish(1, 1, true);

        await action.Should().ThrowAsync<Exception>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateIsSellingBranchDish_WhenDishNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(false);

        var action = async () => await _service.UpdateIsSellingBranchDish(1, 1, true);

        await action.Should().ThrowAsync<Exception>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateIsSellingBranchDish_WhenConfigNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ExistsAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(false);

        var action = async () => await _service.UpdateIsSellingBranchDish(1, 1, true);

        await action.Should().ThrowAsync<Exception>().WithMessage(BranchDishMessage.BranchDishError.BRANCH_DISH_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateIsSellingBranchDish_WhenValid_UpdatesRedisAndReturnsMessage()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.ExistsAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Dishes.ExistsAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ExistsAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(true);

        var result = await _service.UpdateIsSellingBranchDish(1, 1, true);

        result.Should().Be(BranchDishMessage.BranchDishSuccess.BRANCH_DISH_IS_SELLING_UPDATED);
        _mockDishRedisService.Verify(r => r.SetDishSellingStatusAsync(1, 1, true), Times.Once);
    }
    #endregion

    #region 6. SyncDishesToBranchDishConfigAsync
    [Fact]
    public async Task SyncDishesToBranchDishConfigAsync_WhenNoCategories_ReturnsMessage()
    {
        // FindAsync chỉ có 1 tham số
        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(new List<Category>());

        var result = await _service.SyncDishesToBranchDishConfigAsync(Guid.NewGuid());

        result.Should().Be("Không có danh mục nào để đồng bộ.");
    }

    [Fact]
    public async Task SyncDishesToBranchDishConfigAsync_WhenNoDishes_ReturnsMessage()
    {
        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(new List<Category> { new Category { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish>());

        var result = await _service.SyncDishesToBranchDishConfigAsync(Guid.NewGuid());

        result.Should().Be("Không có món ăn nào để đồng bộ.");
    }

    [Fact]
    public async Task SyncDishesToBranchDishConfigAsync_WhenNoRestaurants_ReturnsMessage()
    {
        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(new List<Category> { new Category { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(new List<Dish> { new Dish { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(new List<Restaurant>());

        var result = await _service.SyncDishesToBranchDishConfigAsync(Guid.NewGuid());

        result.Should().Be("Không có nhà hàng (chi nhánh) nào để đồng bộ.");
    }

    [Fact]
    public async Task SyncDishesToBranchDishConfigAsync_WhenConfigsAlreadyExist_ReturnsNoUpdateMessage()
    {
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "test-slug" } };
        var dishes = new List<Dish> { new Dish { Id = 1 } };
        var existingConfigs = new List<BranchDishConfig> { new BranchDishConfig { RestaurantId = 1, DishId = 1 } };

        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(new List<Category> { new Category { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(dishes);
        _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(restaurants);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(existingConfigs);

        var result = await _service.SyncDishesToBranchDishConfigAsync(Guid.NewGuid());

        result.Should().Be("Tất cả các món ăn đã được đồng bộ trước đó, không cần thêm mới.");
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddRangeAsync(It.IsAny<IEnumerable<BranchDishConfig>>()), Times.Never);
    }

    [Fact]
    public async Task SyncDishesToBranchDishConfigAsync_WhenNewConfigsNeeded_AddsAndReturnsSuccessMessage()
    {
        // Arrange
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "test-slug" } };
        var dishes = new List<Dish> { new Dish { Id = 1, Price = 50000m } };
        
        var existingConfigs = new List<BranchDishConfig> 
        { 
            new BranchDishConfig { RestaurantId = 1, DishId = 99 }, 
            new BranchDishConfig { RestaurantId = 99, DishId = 1 }  
        }; 

        _mockUnitOfWork.Setup(u => u.Categories.FindAsync(It.IsAny<Expression<Func<Category, bool>>>())).ReturnsAsync(new List<Category> { new Category { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.Dishes.FindAsync(It.IsAny<Expression<Func<Dish, bool>>>())).ReturnsAsync(dishes);
        _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(restaurants);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FindAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>())).ReturnsAsync(existingConfigs);

        // Act
        var result = await _service.SyncDishesToBranchDishConfigAsync(Guid.NewGuid());

        // Assert
        result.Should().Be("Đã đồng bộ thành công 1 món ăn mới cho các chi nhánh.");
    
        _mockUnitOfWork.Verify(u => u.BranchDishConfigs.AddRangeAsync(It.IsAny<List<BranchDishConfig>>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion
}