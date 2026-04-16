using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Utils;
using System.Linq.Expressions;
using System.Text.Json;
using Xunit;
using AutoMapper;

namespace ScanToOrder.Application.UnitTest.Services;

public class OrderService_CheckoutTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICartRedisService> _mockCartRedisService;
    private readonly Mock<ITransactionRedisService> _mockTransactionRedisService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IMenuCacheService> _mockMenuCacheService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IAuthenticatedUserService> _mockAuthUserService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<Application.Services.OrderService>> _mockLogger;
    private readonly Mock<IQrCodeService> _mockQrCodeService;
    private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
    private readonly Mock<IAIUpsellService> _mockAiUpsellService;

    private readonly Application.Services.OrderService _orderService;

    public OrderService_CheckoutTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCartRedisService = new Mock<ICartRedisService>();
        _mockTransactionRedisService = new Mock<ITransactionRedisService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockMenuCacheService = new Mock<IMenuCacheService>();
        _mockMapper = new Mock<IMapper>();
        _mockAuthUserService = new Mock<IAuthenticatedUserService>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<Application.Services.OrderService>>();
        _mockQrCodeService = new Mock<IQrCodeService>();
        _mockPlanLimitationService = new Mock<IPlanLimitationService>();
        _mockAiUpsellService = new Mock<IAIUpsellService>();

        _orderService = new Application.Services.OrderService(
            _mockUnitOfWork.Object,
            _mockCartRedisService.Object,
            _mockTransactionRedisService.Object,
            _mockRealtimeService.Object,
            _mockMenuCacheService.Object,
            _mockMapper.Object,
            _mockAuthUserService.Object,
            _mockStorageService.Object,
            _mockLogger.Object,
            _mockQrCodeService.Object,
            _mockPlanLimitationService.Object,
            _mockAiUpsellService.Object
        );
    }

    private void SetupValidMocks()
    {
        var validCart = new CartModel
        {
            CartId = "valid-cart",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new CartItemModel { DishId = 1, Quantity = 1, SubTotal = 100000, OriginalPrice = 100000, DiscountedPrice = 100000 }
            },
            TotalAmount = 100000
        };

        var validTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            BankId = Guid.NewGuid(),
            Bank = new Banks { ShortName = "Vietcombank" },
            CardNumber = "1234567890",
            IsVerifyBank = true
        };

        var validRestaurant = new Restaurant
        {
            Id = 1,
            TenantId = validTenant.Id,
            Tenant = validTenant,
            RestaurantName = "Test Rest",
            Slug = "test-rest"
        };

        var validShift = new Shift { Id = 1, RestaurantId = 1, Status = ShiftStatus.Open };

        _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync(JsonSerializer.Serialize(validCart));
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdWithTenantBankAsync(It.IsAny<int>())).ReturnsAsync(validRestaurant);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(validRestaurant);
        _mockUnitOfWork.Setup(u => u.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>())).ReturnsAsync(validShift);
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ReserveDishAvailabilityAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        _mockUnitOfWork.Setup(u => u.Orders.GetNextDailyOrderCodeAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>())).ReturnsAsync(1);
        _mockQrCodeService.Setup(q => q.GenerateQrCodeBytes(It.IsAny<string>())).Returns(new byte[] { 1, 2, 3 });
        _mockStorageService.Setup(s => s.UploadOrderQrAsync(It.IsAny<byte[]>(), It.IsAny<Guid>())).ReturnsAsync("http://qr.com");
        
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
    }

    #region GetPaymentQrAsync
    [Theory]
    // 1. Missing Cart ID
    [InlineData(null, "valid", false, true, true, true, true, false, false, OrderMessage.OrderError.CART_ID_REQUIRED)]
    // 2. Expired JSON
    [InlineData("cart", null, false, true, true, true, true, false, false, OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED)]
    // 3. Invalid JSON data (Handled implicitly, not testable easily via parameters)
    [InlineData("cart", "null", false, true, true, true, true, false, false, OrderMessage.OrderError.INVALID_CART_DATA)]
    // 4. Empty Cart
    [InlineData("cart", "{\"Items\":[]}", false, true, true, true, true, false, false, OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_PAYMENT)]
    // 4.5. Cart Items is null (explicitly)
    [InlineData("cart", "{\"Items\":null}", false, true, true, true, true, false, false, OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_PAYMENT)]
    // 5. Restaurant/Tenant Not Found
    [InlineData("cart", "valid", true, false, true, true, true, false, false, RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND)]
    // 6. No Bank Configured (BankId == null)
    [InlineData("cart", "valid", true, true, false, true, true, false, false, OrderMessage.OrderError.RESTAURANT_NO_BANK_CONFIGURED)]
    // 7. No Bank Configured (Card == null)
    [InlineData("cart", "valid", true, true, true, false, true, false, false, OrderMessage.OrderError.RESTAURANT_NO_BANK_CONFIGURED)]
    // 8. Bank Not Verified
    [InlineData("cart", "valid", true, true, true, true, false, false, false, OrderMessage.OrderError.RESTAURANT_BANK_NOT_VERIFIED)]
    // 9. Phone required
    [InlineData("cart", "valid", true, true, true, true, true, true, false, OrderMessage.OrderError.PHONE_REQUIRED)]
    // 10. PreOrder no Date
    [InlineData("cart", "valid", true, true, true, true, true, false, true, "*RequestedPickupAt*")]
    public async Task GetPaymentQrAsync_BasicValidations_ThrowsDomainException(
        string cartId, string cartJsonTemplate, bool isCartValid, bool isRestaurantValid, bool isBankConfigured, 
        bool isCardConfigured, bool isBankVerified, bool isPhoneEmpty, bool isPreOrderNoDate, string expectedMessage)
    {
        #region Arrange
        SetupValidMocks();

        // Overrides
        if (cartJsonTemplate == null) _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync((string)null);
        else if (cartJsonTemplate == "null") _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync("null");
        else if (!isCartValid) _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync(cartJsonTemplate);
        
        if (!isRestaurantValid) _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdWithTenantBankAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);
        else
        {
            var rest = new Restaurant { Slug = "slug", Tenant = new Tenant { BankId = isBankConfigured ? Guid.NewGuid() : null, Bank = isBankConfigured ? new Banks() : null, CardNumber = isCardConfigured ? "123" : null, IsVerifyBank = isBankVerified } };
            _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdWithTenantBankAsync(It.IsAny<int>())).ReturnsAsync(rest);
        }

        string phone = isPhoneEmpty ? "" : "0123456789";
        #endregion

        #region Act
        var action = async () => await _orderService.GetPaymentQrAsync(cartId, phone, isPreOrderNoDate, null, null);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(expectedMessage);
        #endregion
    }

    [Theory]
    // 1. Shift missing
    [InlineData(1, true, PromotionScope.Order, true, false, true, 50000, ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET)]
    // 2. Promotion Not Found / Invalid state
    [InlineData(1, false, PromotionScope.Order, true, true, true, 50000, "Mã khuyến mãi không hợp lệ.")]
    [InlineData(1, true, PromotionScope.Dish, true, true, true, 50000, "Mã khuyến mãi không hợp lệ.")] // Wrong scope
    // 3. Promotion Time Invalid
    [InlineData(1, true, PromotionScope.Order, false, true, true, 50000, "Mã khuyến mãi đã hết hạn hoặc chưa tới khung giờ áp dụng.")]
    // 4. Min Order Value not met
    [InlineData(1, true, PromotionScope.Order, true, true, true, 200000, "Đơn hàng chưa đạt giá trị tối thiểu 200000 để áp dụng mã.")]
    // 5. Wrong Restaurant boundaries
    [InlineData(1, true, PromotionScope.Order, true, true, false, 50000, "Mã khuyến mãi không áp dụng cho nhà hàng này.")]
    public async Task GetPaymentQrAsync_PromotionAndShiftValidations_ThrowsDomainException(
        int promotionId, bool isPromotionValid, PromotionScope scope, bool isTimeValid, bool isShiftOpen, bool isCorrectRestaurant, decimal minOrder, string expectedMessage)
    {
        #region Arrange
        SetupValidMocks();
        
        if (!isShiftOpen) _mockUnitOfWork.Setup(u => u.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>())).ReturnsAsync((Shift)null);

        var promotion = isPromotionValid ? new Promotion 
        { 
            Id = promotionId, IsDeleted = false, IsActive = true, Scope = scope, MinOrderValue = minOrder, 
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = isTimeValid ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow.AddDays(-1),
            Type = PromotionType.Standard, IsGlobal = isCorrectRestaurant,
            RestaurantPromotions = isCorrectRestaurant ? new List<RestaurantPromotion>() : new List<RestaurantPromotion> { new RestaurantPromotion { RestaurantId = 999 } }
        } : null;

        _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(promotion);
        #endregion

        #region Act
        var action = async () => await _orderService.GetPaymentQrAsync("valid", "0123", false, null, promotionId);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(expectedMessage);
        #endregion
    }

    [Fact]
    public async Task GetPaymentQrAsync_WhenDishIsOutOfStock_ThrowsExceptionAndRollbacks()
    {
        #region Arrange
        SetupValidMocks();
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ReserveDishAvailabilityAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        #endregion

        #region Act
        var action = async () => await _orderService.GetPaymentQrAsync("cart", "012", false, null, null);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>();
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        #endregion
    }

    [Fact]
    public async Task GetPaymentQrAsync_HappyPath_CompletesTransactionAndReturnsDto()
    {
        #region Arrange
        SetupValidMocks();

        // Target RestaurantName = restaurant.RestaurantName ?? "" branch (null case)
        var validTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            BankId = Guid.NewGuid(),
            Bank = new Banks { ShortName = "Vietcombank" },
            CardNumber = "123",
            IsVerifyBank = true
        };
        var restWithNullName = new Restaurant { Id = 1, Tenant = validTenant, TenantId = validTenant.Id, RestaurantName = null, Slug = "test" };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdWithTenantBankAsync(It.IsAny<int>())).ReturnsAsync(restWithNullName);

        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        #endregion

        #region Act
        var result = await _orderService.GetPaymentQrAsync("cart", "012", false, null, null);
        #endregion

        #region Assert
        result.Should().NotBeNull();
        result.QrUrl.Should().NotBeNullOrEmpty();
        
        _mockUnitOfWork.Verify(u => u.Orders.AddAsync(It.IsAny<Order>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.Transactions.AddAsync(It.IsAny<Transaction>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Exactly(2)); // order save + transaction save
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockMenuCacheService.Verify(m => m.InvalidateMenuAsync(It.IsAny<int>()), Times.Once);
        #endregion
    }

    [Fact]
    public async Task GetPaymentQrAsync_HappyPath_WithPromotionAndPreOrder_ReturnsDto()
    {
        #region Arrange
        SetupValidMocks();
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        var promotion = new Promotion 
        { 
            Id = 1, IsDeleted = false, IsActive = true, Scope = PromotionScope.Order, MinOrderValue = 50000, 
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
            Type = PromotionType.Standard, IsGlobal = false,
            RestaurantPromotions = new List<RestaurantPromotion> { new RestaurantPromotion { RestaurantId = 1 } }
        };
        _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(promotion);
        var pickupDate = DateTime.UtcNow.AddHours(2);
        #endregion

        #region Act
        var result = await _orderService.GetPaymentQrAsync("cart", "012", true, pickupDate, 1);
        #endregion

        #region Assert
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.Orders.AddAsync(It.Is<Order>(o => o.IsPreOrder == true && o.RequestedPickupAt == pickupDate && o.PromotionId == 1)), Times.Once);
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        #endregion
    }
    #endregion

    #region CheckoutCashAsync
    [Theory]
    // 1. Missing Cart ID
    [InlineData(null, "valid", false, true, true, OrderMessage.OrderError.CART_ID_REQUIRED)]
    // 2. Missing Phone
    [InlineData("cart", "valid", true, true, true, OrderMessage.OrderError.PHONE_REQUIRED)]
    // 3. Expired JSON
    [InlineData("cart", null, false, true, true, OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED)]
    // 3.5. Invalid JSON data
    [InlineData("cart", "null", false, true, true, OrderMessage.OrderError.INVALID_CART_DATA)]
    // 4. Empty Cart List
    [InlineData("cart", "{\"Items\":[]}", false, true, true, OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_ORDER)]
    // 4.5. Cart Items is null
    [InlineData("cart", "{\"Items\":null}", false, true, true, OrderMessage.OrderError.CART_EMPTY_CANNOT_CREATE_ORDER)]
    // 5. Restaurant Not Found
    [InlineData("cart", "valid", false, false, true, RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND)]
    // 6. Shift missing
    [InlineData("cart", "valid", false, true, false, ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET)]
    public async Task CheckoutCashAsync_BasicValidations_ThrowsDomainException(
        string cartId, string cartJsonTemplate, bool isPhoneEmpty, bool isRestaurantValid, bool isShiftOpen, string expectedMessage)
    {
        #region Arrange
        SetupValidMocks();

        if (cartJsonTemplate == null) _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync((string)null);
        else if (cartJsonTemplate == "null") _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync("null");
        else if (cartJsonTemplate != "valid") _mockCartRedisService.Setup(r => r.GetRawCartAsync(It.IsAny<string>())).ReturnsAsync(cartJsonTemplate);

        if (!isRestaurantValid) _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant)null);

        if (!isShiftOpen) _mockUnitOfWork.Setup(u => u.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>())).ReturnsAsync((Shift)null);

        string phone = isPhoneEmpty ? "" : "0123456789";
        var request = new CashCheckoutRequest { CartId = cartId, Phone = phone };
        #endregion

        #region Act
        var action = async () => await _orderService.CheckoutCashAsync(request);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(expectedMessage);
        #endregion
    }

    [Theory]
    // 1. Promotion Not Found / Invalid state
    [InlineData(1, false, PromotionScope.Order, true, 50000, true, "Mã khuyến mãi không hợp lệ.")]
    [InlineData(1, true, PromotionScope.Dish, true, 50000, true, "Mã khuyến mãi không hợp lệ.")]
    // 2. Promotion Time Invalid
    [InlineData(1, true, PromotionScope.Order, false, 50000, true, "Mã khuyến mãi đã hết hạn hoặc chưa tới khung giờ áp dụng.")]
    // 3. Min Order Value not met
    [InlineData(1, true, PromotionScope.Order, true, 200000, true, "Đơn hàng chưa đạt giá trị tối thiểu 200000 để áp dụng mã.")]
    // 4. Wrong Restaurant boundaries
    [InlineData(1, true, PromotionScope.Order, true, 50000, false, "Mã khuyến mãi không áp dụng cho nhà hàng này.")]
    public async Task CheckoutCashAsync_PromotionValidations_ThrowsDomainException(
        int promotionId, bool isPromotionValid, PromotionScope scope, bool isTimeValid, decimal minOrder, bool isCorrectRestaurant, string expectedMessage)
    {
        #region Arrange
        SetupValidMocks();
        var request = new CashCheckoutRequest { CartId = "valid-cart", Phone = "0123", AppliedPromotionId = promotionId };

        var promotion = isPromotionValid ? new Promotion 
        { 
            Id = promotionId, IsDeleted = false, IsActive = true, Scope = scope, MinOrderValue = minOrder, 
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = isTimeValid ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow.AddDays(-1),
            Type = PromotionType.Standard, IsGlobal = isCorrectRestaurant,
            RestaurantPromotions = isCorrectRestaurant ? new List<RestaurantPromotion>() : new List<RestaurantPromotion> { new RestaurantPromotion { RestaurantId = 999 } }
        } : null;

        _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(promotion);
        #endregion

        #region Act
        var action = async () => await _orderService.CheckoutCashAsync(request);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>().WithMessage(expectedMessage);
        #endregion
    }

    [Fact]
    public async Task CheckoutCashAsync_WhenDishIsOutOfStock_ThrowsExceptionAndRollbacks()
    {
        #region Arrange
        SetupValidMocks();
        _mockUnitOfWork.Setup(u => u.BranchDishConfigs.ReserveDishAvailabilityAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        var request = new CashCheckoutRequest { CartId = "cart", Phone = "0123" };
        #endregion

        #region Act
        var action = async () => await _orderService.CheckoutCashAsync(request);
        #endregion

        #region Assert
        await action.Should().ThrowAsync<DomainException>();
        mockTx.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        #endregion
    }

    [Fact]
    public async Task CheckoutCashAsync_HappyPath_CompletesTransactionAndReturnsDto()
    {
        #region Arrange
        SetupValidMocks();
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        var request = new CashCheckoutRequest { CartId = "cart", Phone = "0123456789" };
        var savedOrder = new Order { Id = Guid.NewGuid(), OrderCode = 999 };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(savedOrder);
        #endregion

        #region Act
        var result = await _orderService.CheckoutCashAsync(request);
        #endregion

        #region Assert
        result.Should().NotBeNull();
        result.OrderCode.Should().Be(999);
        result.Status.Should().Be(OrderStatus.Unpaid);
        
        _mockUnitOfWork.Verify(u => u.Orders.AddAsync(It.IsAny<Order>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.Transactions.AddAsync(It.IsAny<Transaction>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once); 
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRealtimeService.Verify(r => r.SendOrderToKitchen(It.IsAny<string>(), It.IsAny<OrderRealtimeDto>()), Times.Once);
        _mockMenuCacheService.Verify(m => m.InvalidateMenuAsync(It.IsAny<int>()), Times.Once);
        #endregion
    }

    [Fact]
    public async Task CheckoutCashAsync_HappyPath_WithLocalPromotion_ReturnsDto()
    {
        #region Arrange
        SetupValidMocks();
        var mockTx = new Mock<IDbTransaction>();
        _mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        var request = new CashCheckoutRequest { CartId = "cart", Phone = "0123456789", AppliedPromotionId = 1 };
        
        var promotion = new Promotion 
        { 
            Id = 1, IsDeleted = false, IsActive = true, Scope = PromotionScope.Order, MinOrderValue = 50000, 
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
            Type = PromotionType.Standard, IsGlobal = false,
            RestaurantPromotions = new List<RestaurantPromotion> { new RestaurantPromotion { RestaurantId = 1 } }
        };
        _mockUnitOfWork.Setup(u => u.Promotions.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(promotion);

        var savedOrder = new Order { Id = Guid.NewGuid(), OrderCode = 999 };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(savedOrder);
        #endregion

        #region Act
        var result = await _orderService.CheckoutCashAsync(request);
        #endregion

        #region Assert
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.Orders.AddAsync(It.Is<Order>(o => o.PromotionId == 1)), Times.Once);
        mockTx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        #endregion
    }
    #endregion
}
