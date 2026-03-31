using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using System.Text.Json;
using Xunit;
using AutoMapper;

namespace ScanToOrder.Application.UnitTest.Services;

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICartRedisService> _mockCartRedisService;
    private readonly Mock<ITransactionRedisService> _mockTransactionRedisService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IAuthenticatedUserService> _mockAuthUserService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<OrderService>> _mockLogger;
    private readonly Mock<IQrCodeService> _mockQrCodeService;

    private readonly OrderService _orderService;

    private readonly AddToCartRequest _validRequest;
    private readonly Restaurant _validRestaurant;
    private readonly BranchDishConfig _validBranchDish;
    private readonly Dish _validDish;

    public OrderServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCartRedisService = new Mock<ICartRedisService>();
        _mockTransactionRedisService = new Mock<ITransactionRedisService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockMapper = new Mock<IMapper>();
        _mockAuthUserService = new Mock<IAuthenticatedUserService>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<OrderService>>();
        _mockQrCodeService = new Mock<IQrCodeService>();

        _orderService = new OrderService(
            _mockUnitOfWork.Object,
            _mockCartRedisService.Object,
            _mockTransactionRedisService.Object,
            _mockRealtimeService.Object,
            _mockMapper.Object,
            _mockAuthUserService.Object,
            _mockStorageService.Object,
            _mockLogger.Object,
            _mockQrCodeService.Object
        );

        // Setup common valid objects
        _validRequest = new AddToCartRequest { Quantity = 2, RestaurantId = 1, DishId = 10, CartId = "test-cart-id" };
        
        _validRestaurant = new Restaurant
        {
            Id = 1,
            TenantId = Guid.NewGuid(),
            Slug = null
        };
        
        _validDish = new Dish { Id = 10, DishName = "Test Dish" };
        
        _validBranchDish = new BranchDishConfig 
        { 
            RestaurantId = 1, 
            DishId = 10, 
            IsSelling = true, 
            IsSoldOut = false, 
            Price = 50000,
            Dish = _validDish
        };

        // Setup Base Mocks (Happy Path)
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(_validRestaurant);
        _mockUnitOfWork.Setup(u => u.Dishes.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(_validDish);
        
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(_validBranchDish);
            
        // Mock internal call for GetDishesByIdsWithPromotionAsync
        _mockUnitOfWork.Setup(u => u.Promotions.GetAllAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(new List<Promotion>());
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(It.IsAny<int>(), It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig> { _validBranchDish });

        _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync((string)null); // new cart
        
        _mockMapper.Setup(m => m.Map<CartDto>(It.IsAny<CartModel>())).Returns(new CartDto());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task AddToCartAsync_WhenQuantityIsInvalid_ThrowsDomainException(int invalidQuantity)
    {
        #region Arrange
        var request = new AddToCartRequest { Quantity = invalidQuantity, RestaurantId = 1, DishId = 1 };
        #endregion

        #region Act
        var action = async () => await _orderService.AddToCartAsync(request);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.QUANTITY_MUST_BE_GREATER_THAN_ZERO);
        #endregion
    }

    [Theory]
    // 1. RestaurantNotFound
    [InlineData(false, true, true, true, false, 0, RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND)]
    // 2. DishNotFound (BranchDish == null)
    [InlineData(true, true, false, true, false, 0, DishMessage.DishError.DISH_NOT_FOUND)]
    // 3. BranchDish NotSelling
    [InlineData(true, true, true, false, false, 0, BranchDishMessage.BranchDishError.NOT_SELLING)]
    // 4. BranchDish SoldOut
    [InlineData(true, true, true, true, true, 0, BranchDishMessage.BranchDishError.SOLD_OUT)]
    // 5. DishNotFound (Dish entity == null)
    [InlineData(true, false, true, true, false, 0, DishMessage.DishError.DISH_NOT_FOUND)]
    // 6. Cart exists for different restaurant
    [InlineData(true, true, true, true, false, 999, OrderMessage.OrderError.CANNOT_ADD_DISH_FROM_OTHER_RESTAURANT)]
    public async Task AddToCartAsync_WhenValidationFails_ThrowsDomainException(
        bool restaurantExists, bool dishExists, bool branchDishExists, bool isSelling, bool isSoldOut, int existingCartRestaurantId, string expectedMessage)
    {
        #region Arrange
        if (!restaurantExists)
            _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);

        if (!dishExists)
            _mockUnitOfWork.Setup(u => u.Dishes.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Dish)null);

        if (!branchDishExists)
            _mockUnitOfWork.Setup(u => u.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync((BranchDishConfig)null);

        _validBranchDish.IsSelling = isSelling;
        _validBranchDish.IsSoldOut = isSoldOut;

        if (existingCartRestaurantId != 0)
        {
            var existingCart = new CartModel { RestaurantId = existingCartRestaurantId };
            _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync(JsonSerializer.Serialize(existingCart));
        }
        #endregion

        #region Act
        var action = async () => await _orderService.AddToCartAsync(_validRequest);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(expectedMessage);
        #endregion
    }

    [Theory]
    [InlineData(null, null, null, false)]          // cartId is null, existing JSON is null, Note is null, New item
    [InlineData("   ", null, "No spicy", false)]   // whitespace cartId -> generated new, existing JSON is null, Note provided
    [InlineData("test-cart", "null", "Note", false)] // "null" JSON string triggers the '?? new CartModel' branch
    [InlineData("test-cart", "{\"CartId\":\"test-cart\",\"RestaurantId\":1,\"Items\":[]}", null, false)] // empty cart JSON triggers new item block
    [InlineData("test-cart", "{\"CartId\":\"test-cart\",\"RestaurantId\":1,\"Items\":[{\"DishId\":10,\"Quantity\":1,\"DiscountedPrice\":50000,\"OriginalPrice\":50000,\"SubTotal\":50000}]}", null, true)] // existing item updates quantity
    public async Task AddToCartAsync_CartStateCombinations_Succeeds(
        string cartIdInput, string existingCartJson, string noteInput, bool itemExistsAlready)
    {
        #region Arrange
        var request = new AddToCartRequest 
        { 
            Quantity = 2, 
            RestaurantId = 1, 
            DishId = 10, 
            CartId = cartIdInput, 
            Note = noteInput 
        };
        _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync(existingCartJson);
        #endregion

        #region Act
        var result = await _orderService.AddToCartAsync(request);
        #endregion

        #region Assert
        _mockCartRedisService.Verify(r => r.SaveRawCartAsync(
            It.Is<string>(id => !string.IsNullOrWhiteSpace(id) && (string.IsNullOrWhiteSpace(cartIdInput) ? id.Length == 32 : id == cartIdInput)), 
            It.Is<string>(json => 
                (noteInput == null || json.Contains($"\"Note\":\"{noteInput}\"")) && 
                (itemExistsAlready ? json.Contains($"\"Quantity\":3") : json.Contains($"\"Quantity\":2"))
            ), 
            It.IsAny<TimeSpan?>()), Times.AtLeastOnce());
            
        _mockMapper.Verify(m => m.Map<CartDto>(It.IsAny<CartModel>()), Times.Once);
        #endregion
    }
}
