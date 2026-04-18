using System.Reflection;
using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using Xunit;

namespace ScanToOrder.Application.UnitTest.Services.OrderService;

public class OrderService_MissingCoverageTests
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

    private readonly Application.Services.OrderService _service;

    public OrderService_MissingCoverageTests()
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

        _service = new Application.Services.OrderService(
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

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(It.IsAny<int>()))
            .ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = false });
    }

    [Fact]
    public async Task UpdateCartItemQuantity_NewQuantityZero_RemovesItemAndSaves()
    {
        var cart = new CartModel
        {
            CartId = "c1",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 2, DiscountedPrice = 10000, SubTotal = 20000 }
            },
            TotalAmount = 20000
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c1")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, TenantId = Guid.NewGuid(), Slug = "r1" });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(1, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>());
        _mockMapper.Setup(x => x.Map<CartDto>(It.IsAny<CartModel>())).Returns(new CartDto());

        var result = await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest { CartId = "c1", DishId = 10, NewQuantity = 0 });

        result.Should().NotBeNull();
        _mockCartRedisService.Verify(x => x.SaveRawCartAsync("c1", It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("c", 1, -1)]
    public async Task UpdateCartItemQuantity_InvalidRequest_ThrowsDomainException(string cartId, int dishId, int newQty)
    {
        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = cartId,
            DishId = dishId,
            NewQuantity = newQty
        });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenBranchDishSoldOut_Throws()
    {
        var cart = new CartModel
        {
            CartId = "c3",
            RestaurantId = 1,
            Items = new List<CartItemModel> { new() { DishId = 10, Quantity = 1, DiscountedPrice = 5000, SubTotal = 5000 } }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c3")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new BranchDishConfig { RestaurantId = 1, DishId = 10, IsSelling = true, IsSoldOut = true });

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest { CartId = "c3", DishId = 10, NewQuantity = 2 });

        await act.Should().ThrowAsync<DomainException>().WithMessage(BranchDishMessage.BranchDishError.SOLD_OUT);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenCartNotFound_Throws()
    {
        _mockCartRedisService.Setup(x => x.GetRawCartAsync("missing-cart")).ReturnsAsync(string.Empty);

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "missing-cart",
            DishId = 10,
            NewQuantity = 1
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.CART_NOT_FOUND_OR_EXPIRED);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenCartJsonInvalid_Throws()
    {
        _mockCartRedisService.Setup(x => x.GetRawCartAsync("invalid-cart")).ReturnsAsync("null");

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "invalid-cart",
            DishId = 10,
            NewQuantity = 1
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.INVALID_CART_DATA);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenItemNotFoundInCart_Throws()
    {
        var cart = new CartModel
        {
            CartId = "c-item",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 11, Quantity = 1, DiscountedPrice = 10000, SubTotal = 10000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c-item")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "c-item",
            DishId = 10,
            NewQuantity = 1
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ITEM_NOT_FOUND_IN_CART);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenBranchDishNotFound_Throws()
    {
        var cart = new CartModel
        {
            CartId = "c-null-branch",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 1, DiscountedPrice = 10000, SubTotal = 10000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c-null-branch")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync((BranchDishConfig)null);

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "c-null-branch",
            DishId = 10,
            NewQuantity = 2
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(DishMessage.DishError.DISH_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenBranchDishNotSelling_Throws()
    {
        var cart = new CartModel
        {
            CartId = "c-not-sell",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 1, DiscountedPrice = 10000, SubTotal = 10000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c-not-sell")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new BranchDishConfig { RestaurantId = 1, DishId = 10, IsSelling = false, IsSoldOut = false });

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "c-not-sell",
            DishId = 10,
            NewQuantity = 2
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(BranchDishMessage.BranchDishError.NOT_SELLING);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenValidQuantity_UpdatesQuantityAndSubtotal()
    {
        var cart = new CartModel
        {
            CartId = "c-success",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 1, DiscountedPrice = 12000, OriginalPrice = 12000, SubTotal = 12000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c-success")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new BranchDishConfig
            {
                RestaurantId = 1,
                DishId = 10,
                IsSelling = true,
                IsSoldOut = false,
                DishAvailability = 10
            });

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = false });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { Id = 1, TenantId = Guid.NewGuid(), Slug = "r1" });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(1, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 1,
                    DishId = 10,
                    Price = 12000,
                    DishAvailability = 10,
                    IsSelling = true,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 10,
                        DishName = "Dish 10",
                        Description = "desc",
                        ImageUrl = "img",
                        Type = DishType.Single
                    }
                }
            });
        _mockMapper.Setup(x => x.Map<CartDto>(It.IsAny<CartModel>())).Returns(new CartDto());

        await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "c-success",
            DishId = 10,
            NewQuantity = 3
        });

        _mockCartRedisService.Verify(x => x.SaveRawCartAsync(
            "c-success",
            It.Is<string>(json => json.Contains("\"Quantity\":3") && json.Contains("\"SubTotal\":36000")),
            It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenDishAvailabilityIsZero_UpdatesWithoutStockCap()
    {
        var cart = new CartModel
        {
            CartId = "c-unlimited",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 1, DiscountedPrice = 10000, OriginalPrice = 10000, SubTotal = 10000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c-unlimited")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new BranchDishConfig
            {
                RestaurantId = 1,
                DishId = 10,
                IsSelling = true,
                IsSoldOut = false,
                DishAvailability = 0
            });

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(1))
            .ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = false });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { Id = 1, TenantId = Guid.NewGuid(), Slug = "r1" });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(1, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 1,
                    DishId = 10,
                    Price = 10000,
                    DishAvailability = 0,
                    IsSelling = true,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 10,
                        DishName = "Dish 10",
                        Description = "desc",
                        ImageUrl = "img",
                        Type = DishType.Single
                    }
                }
            });
        _mockMapper.Setup(x => x.Map<CartDto>(It.IsAny<CartModel>())).Returns(new CartDto());

        await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest
        {
            CartId = "c-unlimited",
            DishId = 10,
            NewQuantity = 50
        });

        _mockCartRedisService.Verify(x => x.SaveRawCartAsync(
            "c-unlimited",
            It.Is<string>(json => json.Contains("\"Quantity\":50") && json.Contains("\"SubTotal\":500000")),
            It.IsAny<TimeSpan?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_ExceedsStock_ThrowsDomainException()
    {
        var cart = new CartModel
        {
            CartId = "c2",
            RestaurantId = 1,
            Items = new List<CartItemModel>
            {
                new() { DishId = 10, Quantity = 1, DiscountedPrice = 10000, SubTotal = 10000 }
            }
        };

        _mockCartRedisService.Setup(x => x.GetRawCartAsync("c2")).ReturnsAsync(System.Text.Json.JsonSerializer.Serialize(cart));
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.FirstOrDefaultAsync(It.IsAny<Expression<Func<BranchDishConfig, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new BranchDishConfig { DishId = 10, RestaurantId = 1, IsSelling = true, IsSoldOut = false, DishAvailability = 2 });

        var act = async () => await _service.UpdateCartItemQuantityAsync(new UpdateCartItemRequest { CartId = "c2", DishId = 10, NewQuantity = 3 });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetCashOrdersPendingConfirm_WhenOrdersNull_ReturnsEmpty()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId)).ReturnsAsync(new Staff { Id = staffId, RestaurantId = 1 });
        _mockUnitOfWork.Setup(x => x.Orders.GetCashOrdersPendingConfirmAsync(1)).ReturnsAsync((List<Order>)null);

        var result = await _service.GetCashOrdersPendingConfirmAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCashOrdersPendingConfirm_WhenStaffNotIdentified_Throws()
    {
        _mockAuthUserService.Setup(x => x.ProfileId).Returns((Guid?)null);

        var act = async () => await _service.GetCashOrdersPendingConfirmAsync();

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);
    }

    [Fact]
    public async Task GetCashOrdersPendingConfirm_WhenStaffNotFound_Throws()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId)).ReturnsAsync((Staff)null);

        var act = async () => await _service.GetCashOrdersPendingConfirmAsync();

        await act.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task GetCashOrdersPendingConfirm_WithOrder_MapsItems()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId)).ReturnsAsync(new Staff { Id = staffId, RestaurantId = 1 });
        _mockUnitOfWork.Setup(x => x.Orders.GetCashOrdersPendingConfirmAsync(1)).ReturnsAsync(new List<Order>
        {
            new()
            {
                Id = Guid.NewGuid(),
                OrderCode = 12,
                TotalAmount = 100,
                FinalAmount = 90,
                PromotionDiscount = 10,
                NumberPhone = "0123",
                Type = "Cash",
                OrderDetails = new List<OrderDetail>
                {
                    new() { Quantity = 1, SubTotal = 90, Dish = new Dish { DishName = "Pho" } }
                }
            }
        });

        var result = await _service.GetCashOrdersPendingConfirmAsync();

        result.Should().HaveCount(1);
        result[0].Items.Should().HaveCount(1);
        result[0].Items[0].DishName.Should().Be("Pho");
    }

    [Fact]
    public async Task EnsureOrderInStaffRestaurant_OrderNotInRestaurant_Throws()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId)).ReturnsAsync(new Staff { Id = staffId, RestaurantId = 3 });
        _mockUnitOfWork.Setup(x => x.Orders.GetByOrderCodeAndRestaurantAsync(111, 3)).ReturnsAsync((Order)null);

        var act = async () => await _service.EnsureOrderInStaffRestaurantAsync(111);

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_SEQUENCE_NOT_FOUND_IN_RESTAURANT);
    }

    [Fact]
    public async Task EnsureOrderInStaffRestaurant_WhenProfileMissing_Throws()
    {
        _mockAuthUserService.Setup(x => x.ProfileId).Returns((Guid?)null);

        var act = async () => await _service.EnsureOrderInStaffRestaurantAsync(1);

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.STAFF_NOT_IDENTIFIED);
    }

    [Fact]
    public async Task EnsureOrderInStaffRestaurant_WhenStaffNotFound_Throws()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId)).ReturnsAsync((Staff)null);

        var act = async () => await _service.EnsureOrderInStaffRestaurantAsync(1);

        await act.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task EnsureOrderInStaffRestaurant_WhenOrderExists_CompletesSuccessfully()
    {
        var staffId = Guid.NewGuid();
        _mockAuthUserService.Setup(x => x.ProfileId).Returns(staffId);
        _mockUnitOfWork.Setup(x => x.Staffs.GetByIdAsync(staffId))
            .ReturnsAsync(new Staff { Id = staffId, RestaurantId = 5 });
        _mockUnitOfWork.Setup(x => x.Orders.GetByOrderCodeAndRestaurantAsync(222, 5))
            .ReturnsAsync(new Order { Id = Guid.NewGuid(), OrderCode = 222, RestaurantId = 5 });

        var act = async () => await _service.EnsureOrderInStaffRestaurantAsync(222);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessOrderPayment_NoShiftIdAndNoActiveShift_StillProcessesSuccessfully()
    {
        var orderId = Guid.NewGuid();
        var txEntity = new Transaction { TransactionCode = "PAY1", Status = OrderTransactionStatus.Pending, ShiftId = null };
        var order = new Order { Id = orderId, RestaurantId = 1, OrderCode = 123, FinalAmount = 50000, Status = OrderStatus.Unpaid };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(txEntity);
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY1")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(order);
        _mockUnitOfWork.Setup(x => x.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>())).ReturnsAsync((Shift)null);
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);
        _mockStorageService.Setup(x => x.GetOrGeneratePaymentReceivedAudioAsync(123, 50000)).ReturnsAsync("audio-url");

        await _service.ProcessOrderPaymentAsync("PAY1", 50000);

        order.Status.Should().Be(OrderStatus.Pending);
        txEntity.Status.Should().Be(OrderTransactionStatus.Success);
        _mockTransactionRedisService.Verify(x => x.DeleteOrderPaymentCodeAsync("PAY1"), Times.Once);
        dbTx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", 1000)]
    [InlineData("PAY", 0)]
    public async Task ProcessOrderPayment_InvalidInput_Throws(string paymentCode, decimal amount)
    {
        var act = async () => await _service.ProcessOrderPaymentAsync(paymentCode, amount);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenTransactionNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync((Transaction)null);

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY1", 1000);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenTransactionAlreadySuccess_ReturnsEarly()
    {
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-S", Status = OrderTransactionStatus.Success });

        await _service.ProcessOrderPaymentAsync("PAY-S", 1000);

        _mockTransactionRedisService.Verify(x => x.GetCartIdByOrderPaymentCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenOrderIdStringMissing_Throws()
    {
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-M", Status = OrderTransactionStatus.Pending });
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-M")).ReturnsAsync("  ");

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY-M", 1000);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_FROM_PAYMENT_CODE_NOT_FOUND_OR_EXPIRED);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenOrderIdStringInvalidGuid_Throws()
    {
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-G", Status = OrderTransactionStatus.Pending });
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-G")).ReturnsAsync("not-guid");

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY-G", 1000);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.INVALID_ORDER_CODE);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenOrderNotFound_Throws()
    {
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-O", Status = OrderTransactionStatus.Pending });
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-O")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync((Order)null);

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY-O", 1000);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenOrderIsNotUnpaid_ReturnsEarly()
    {
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-R", Status = OrderTransactionStatus.Pending });
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-R")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(new Order { Id = orderId, Status = OrderStatus.Pending });

        await _service.ProcessOrderPaymentAsync("PAY-R", 1000);

        _mockUnitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenTransferAmountMismatch_Throws()
    {
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Transaction { TransactionCode = "PAY-AM", Status = OrderTransactionStatus.Pending });
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-AM")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(new Order
        {
            Id = orderId,
            Status = OrderStatus.Unpaid,
            FinalAmount = 20000
        });

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY-AM", 19000);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.PAYMENT_AMOUNT_MISMATCH);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenNoShiftIdAndActiveShiftExists_AssignsShiftId()
    {
        var orderId = Guid.NewGuid();
        var txEntity = new Transaction { TransactionCode = "PAY-SH", Status = OrderTransactionStatus.Pending, ShiftId = null };
        var order = new Order { Id = orderId, RestaurantId = 1, OrderCode = 321, FinalAmount = 5000, Status = OrderStatus.Unpaid };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(txEntity);
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-SH")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(order);
        _mockUnitOfWork.Setup(x => x.Shifts.FirstOrDefaultAsync(It.IsAny<Expression<Func<Shift, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(new Shift { Id = 9, RestaurantId = 1, Status = ShiftStatus.Open });
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);
        _mockStorageService.Setup(x => x.GetOrGeneratePaymentReceivedAudioAsync(321, 5000)).ReturnsAsync("audio");

        await _service.ProcessOrderPaymentAsync("PAY-SH", 5000);

        txEntity.ShiftId.Should().Be(9);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenAudioGenerationFails_StillNotifiesWithEmptyAudio()
    {
        var orderId = Guid.NewGuid();
        var txEntity = new Transaction { TransactionCode = "PAY-AUDIO", Status = OrderTransactionStatus.Pending, ShiftId = 1 };
        var order = new Order { Id = orderId, RestaurantId = 2, OrderCode = 456, FinalAmount = 8000, Status = OrderStatus.Unpaid };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(txEntity);
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY-AUDIO")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(order);
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);
        _mockStorageService.Setup(x => x.GetOrGeneratePaymentReceivedAudioAsync(456, 8000)).ThrowsAsync(new Exception("audio fail"));

        await _service.ProcessOrderPaymentAsync("PAY-AUDIO", 8000);

        _mockRealtimeService.Verify(x => x.NotifyPaymentReceived("2", 456, 8000, ""), Times.Once);
        dbTx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderPayment_WhenSaveFails_RollbacksTransaction()
    {
        var orderId = Guid.NewGuid();
        var txEntity = new Transaction { TransactionCode = "PAY2", Status = OrderTransactionStatus.Pending, ShiftId = 1 };
        var order = new Order { Id = orderId, RestaurantId = 1, OrderCode = 123, FinalAmount = 50000, Status = OrderStatus.Unpaid };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Transactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<Transaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(txEntity);
        _mockTransactionRedisService.Setup(x => x.GetCartIdByOrderPaymentCodeAsync("PAY2")).ReturnsAsync(orderId.ToString());
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(orderId)).ReturnsAsync(order);
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);
        _mockUnitOfWork.Setup(x => x.SaveAsync()).ThrowsAsync(new Exception("db fail"));

        var act = async () => await _service.ProcessOrderPaymentAsync("PAY2", 50000);

        await act.Should().ThrowAsync<Exception>().WithMessage("db fail");
        dbTx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCustomerActiveOrders_InvalidInput_Throws()
    {
        var act1 = async () => await _service.GetCustomerActiveOrdersAsync(0, "0123");
        var act2 = async () => await _service.GetCustomerActiveOrdersAsync(1, "   ");

        await act1.Should().ThrowAsync<DomainException>();
        await act2.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetCustomerActiveOrders_TrimsPhoneAndMapsDtos()
    {
        _mockUnitOfWork.Setup(x => x.Orders.GetCustomerActiveOrdersAsync(1, "0123"))
            .ReturnsAsync(new List<Order>());
        _mockMapper.Setup(x => x.Map<List<CustomerOrderSummaryDto>>(It.IsAny<List<Order>>()))
            .Returns(new List<CustomerOrderSummaryDto>());

        var result = await _service.GetCustomerActiveOrdersAsync(1, " 0123 ");

        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(x => x.Orders.GetCustomerActiveOrdersAsync(1, "0123"), Times.Once);
    }

    [Fact]
    public async Task GetCustomerActiveOrdersAllRestaurants_InvalidPhone_Throws()
    {
        var act = async () => await _service.GetCustomerActiveOrdersAllRestaurantsAsync("   ");
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetCustomerActiveOrdersAllRestaurants_TrimsPhoneAndMapsDtos()
    {
        _mockUnitOfWork.Setup(x => x.Orders.GetCustomerActiveOrdersAllRestaurantsAsync("0987"))
            .ReturnsAsync(new List<Order>());
        _mockMapper.Setup(x => x.Map<List<CustomerOrderSummaryDto>>(It.IsAny<List<Order>>()))
            .Returns(new List<CustomerOrderSummaryDto>());

        var result = await _service.GetCustomerActiveOrdersAllRestaurantsAsync(" 0987 ");

        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(x => x.Orders.GetCustomerActiveOrdersAllRestaurantsAsync("0987"), Times.Once);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenDishIdsEmpty_Throws()
    {
        var act = async () => await _service.GetDishesByIdsWithPromotionAsync(1, new List<int>());
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.DISH_ID_LIST_REQUIRED);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenDishIdsNull_Throws()
    {
        var act = async () => await _service.GetDishesByIdsWithPromotionAsync(1, null!);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.DISH_ID_LIST_REQUIRED);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenRestaurantNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(999)).ReturnsAsync((Restaurant)null);

        var act = async () => await _service.GetDishesByIdsWithPromotionAsync(999, new List<int> { 10 });
        await act.Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenPromotionDisabled_MapsComboItemsWithoutPromotion()
    {
        var tenantId = Guid.NewGuid();
        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(1)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = false });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1, TenantId = tenantId, Slug = "r1" });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(1, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 1,
                    DishId = 30,
                    Price = 25000,
                    DishAvailability = 4,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 30,
                        DishName = "Combo A",
                        Description = "Combo",
                        ImageUrl = "img",
                        Type = DishType.Combo,
                        ComboDetails = new List<ComboDetail>
                        {
                            new() { ItemDishId = 31, Quantity = 2, ItemDish = new Dish { Id = 31, DishName = "Item", ImageUrl = "item-img" } }
                        }
                    }
                }
            });

        var result = await _service.GetDishesByIdsWithPromotionAsync(1, new List<int> { 30 });

        result.Should().HaveCount(1);
        result[0].PromotionName.Should().BeNull();
        result[0].ComboItems.Should().HaveCount(1);
        result[0].ComboItems[0].DishId.Should().Be(31);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenPercentagePromoWins_MapsDiscountAndLabel()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var basePromo = new Promotion
        {
            Name = "Base5",
            TenantId = tenantId,
            IsActive = true,
            IsDeleted = false,
            Scope = PromotionScope.Dish,
            Type = PromotionType.Standard,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 5,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            Priority = 1
        };

        var specificPromo = new Promotion
        {
            Name = "Specific10",
            TenantId = tenantId,
            IsActive = true,
            IsDeleted = false,
            Scope = PromotionScope.Dish,
            Type = PromotionType.Standard,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            Priority = 5
        };

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(2)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(2)).ReturnsAsync(new Restaurant { Id = 2, TenantId = tenantId, Slug = "r2" });
        _mockUnitOfWork.Setup(x => x.Promotions.GetAllAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(new List<Promotion> { basePromo });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(2, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 2,
                    DishId = 40,
                    Price = 20000,
                    DishAvailability = 10,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 40,
                        DishName = "Dish40",
                        Description = "desc",
                        ImageUrl = "img",
                        Type = DishType.Single,
                        PromotionDishes = new List<PromotionDish>
                        {
                            new() { DishId = 40, Promotion = specificPromo }
                        }
                    }
                }
            });

        var result = await _service.GetDishesByIdsWithPromotionAsync(2, new List<int> { 40 });

        result.Should().HaveCount(1);
        result[0].PromotionName.Should().Be("Specific10");
        result[0].PromotionLabel.Should().Be("-10%");
        result[0].DiscountedPrice.Should().Be(18000);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenFixedAmountPromoWins_MapsFixedLabel()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var fixedPromo = new Promotion
        {
            Name = "Fixed",
            TenantId = tenantId,
            IsActive = true,
            IsDeleted = false,
            Scope = PromotionScope.Dish,
            Type = PromotionType.Standard,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 2000,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            Priority = 3
        };

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(3)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(3)).ReturnsAsync(new Restaurant { Id = 3, TenantId = tenantId, Slug = "r3" });
        _mockUnitOfWork.Setup(x => x.Promotions.GetAllAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(new List<Promotion> { fixedPromo });
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(3, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 3,
                    DishId = 50,
                    Price = 22000,
                    DishAvailability = 10,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 50,
                        DishName = "Dish50",
                        Description = "desc",
                        ImageUrl = "img",
                        Type = DishType.Single,
                        PromotionDishes = new List<PromotionDish>()
                    }
                }
            });

        var result = await _service.GetDishesByIdsWithPromotionAsync(3, new List<int> { 50 });

        result.Should().HaveCount(1);
        result[0].PromotionLabel.Should().Be("-2k");
        result[0].DiscountedPrice.Should().Be(20000);
    }

    [Fact]
    public async Task GetDishesByIdsWithPromotion_WhenSpecificPromoFilteredOut_ReturnsNoPromotion()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var invalidSpecificPromo = new Promotion
        {
            Name = "InvalidSpecific",
            TenantId = tenantId,
            IsActive = false,
            IsDeleted = false,
            Scope = PromotionScope.Order,
            Type = PromotionType.Standard,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 10,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            Priority = 9
        };

        _mockPlanLimitationService.Setup(x => x.GetRestaurantFeaturesAsync(4)).ReturnsAsync(new PlanFeaturesConfig { CanUsePromotions = true });
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdAsync(4)).ReturnsAsync(new Restaurant { Id = 4, TenantId = tenantId, Slug = "r4" });
        _mockUnitOfWork.Setup(x => x.Promotions.GetAllAsync(It.IsAny<Expression<Func<Promotion, bool>>>(), It.IsAny<Expression<Func<Promotion, object>>[]>()))
            .ReturnsAsync(new List<Promotion>());
        _mockUnitOfWork.Setup(x => x.BranchDishConfigs.GetSellingDishesByRestaurantIdAndDishIdsAsync(4, It.IsAny<List<int>>()))
            .ReturnsAsync(new List<BranchDishConfig>
            {
                new()
                {
                    RestaurantId = 4,
                    DishId = 60,
                    Price = 25000,
                    DishAvailability = 10,
                    IsSoldOut = false,
                    Dish = new Dish
                    {
                        Id = 60,
                        DishName = "Dish60",
                        Description = "desc",
                        ImageUrl = "img",
                        Type = DishType.Single,
                        PromotionDishes = new List<PromotionDish>
                        {
                            new() { DishId = 60, Promotion = invalidSpecificPromo }
                        }
                    }
                }
            });

        var result = await _service.GetDishesByIdsWithPromotionAsync(4, new List<int> { 60 });

        result.Should().HaveCount(1);
        result[0].PromotionName.Should().BeNull();
        result[0].DiscountedPrice.Should().Be(25000);
    }

    [Fact]
    public async Task ValidateQrCode_WhenReady_UpdatesStatusAndReturnsAudioUrl()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(new Order { Id = id, Status = OrderStatus.Ready });
        _mockStorageService.Setup(x => x.GetOrGenerateScanAudioAsync(777, It.IsAny<string>())).ReturnsAsync("scan-audio");

        var result = await _service.ValidateQrCodeAsync(id.ToString(), 777);

        result.Should().Be("scan-audio");
        _mockUnitOfWork.Verify(x => x.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidateQrCode_WhenQrInvalid_Throws()
    {
        var act = async () => await _service.ValidateQrCodeAsync("", 1);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.QR_INVALID);
    }

    [Fact]
    public async Task ValidateQrCode_WhenNotGuid_Throws()
    {
        var act = async () => await _service.ValidateQrCodeAsync("abc", 1);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.QR_ORDER_ID_INVALID);
    }

    [Fact]
    public async Task ValidateQrCode_WhenOrderNotFound_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync((Order)null);

        var act = async () => await _service.ValidateQrCodeAsync(id.ToString(), 100);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
    }

    [Fact]
    public async Task ValidateQrCode_WhenAlreadyServed_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(new Order { Id = id, Status = OrderStatus.Served });

        var act = async () => await _service.ValidateQrCodeAsync(id.ToString(), 99);
        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.QR_ALREADY_SCANNED);
    }

    [Fact]
    public async Task ValidateQrCode_WhenOrderNotReady_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(new Order { Id = id, Status = OrderStatus.Pending });

        var act = async () => await _service.ValidateQrCodeAsync(id.ToString(), 10);

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_READY);
    }

    [Fact]
    public async Task CancelExpiredUnpaidOrders_NoExpiredOrders_ReturnsImmediately()
    {
        _mockUnitOfWork.Setup(x => x.Orders.GetExpiredUnpaidOrdersAsync(15)).ReturnsAsync(new List<Order>());

        await _service.CancelExpiredUnpaidOrdersAsync();

        _mockUnitOfWork.Verify(x => x.SaveAsync(), Times.Never);
    }

    [Fact]
    public async Task CancelExpiredUnpaidOrders_WithExpiredOrder_DeletesOrderAndCommits()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = 1,
            OrderDetails = new List<OrderDetail>
            {
                new() { DishId = 10, Quantity = 2, Dish = new Dish { Id = 10, DishName = "D1", Type = DishType.Single } }
            }
        };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Orders.GetExpiredUnpaidOrdersAsync(15)).ReturnsAsync(new List<Order> { order });
        _mockUnitOfWork.Setup(x => x.Transactions.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>()))
            .ReturnsAsync(new List<Transaction> { new() { OrderId = order.Id } });
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);

        await _service.CancelExpiredUnpaidOrdersAsync();

        _mockUnitOfWork.Verify(x => x.Orders.Delete(order), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveAsync(), Times.Once);
        dbTx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelExpiredUnpaidOrders_WithComboAndDuplicateDishes_RefundsExpandedQuantities()
    {
        var comboDish = new Dish { Id = 100, DishName = "Combo", Type = DishType.Combo };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = 5,
            OrderDetails = new List<OrderDetail>
            {
                new() { DishId = 100, Quantity = 2, Dish = comboDish },
                new() { DishId = 100, Quantity = 1, Dish = comboDish }
            }
        };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Orders.GetExpiredUnpaidOrdersAsync(15)).ReturnsAsync(new List<Order> { order });
        _mockUnitOfWork.Setup(x => x.ComboDetails.FindAsync(It.IsAny<Expression<Func<ComboDetail, bool>>>() ))
            .ReturnsAsync(new List<ComboDetail>
            {
                new() { DishId = 100, ItemDishId = 200, Quantity = 2 }
            });
        _mockUnitOfWork.Setup(x => x.Transactions.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>() ))
            .ReturnsAsync(new List<Transaction>());
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);

        await _service.CancelExpiredUnpaidOrdersAsync();

        _mockUnitOfWork.Verify(x => x.BranchDishConfigs.RefundDishAvailabilityBatchAsync(
            5,
            It.Is<Dictionary<int, int>>(d => d[100] == 3 && d[200] == 6)), Times.Once);
        dbTx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelExpiredUnpaidOrders_WithNullDishDetail_StillProcesses()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = 6,
            OrderDetails = new List<OrderDetail>
            {
                new() { DishId = 300, Quantity = 1, Dish = null! }
            }
        };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Orders.GetExpiredUnpaidOrdersAsync(15)).ReturnsAsync(new List<Order> { order });
        _mockUnitOfWork.Setup(x => x.Transactions.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>() ))
            .ReturnsAsync(new List<Transaction>());
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);

        await _service.CancelExpiredUnpaidOrdersAsync();

        _mockUnitOfWork.Verify(x => x.BranchDishConfigs.RefundDishAvailabilityBatchAsync(
            6,
            It.Is<Dictionary<int, int>>(d => d[300] == 1)), Times.Once);
        dbTx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelExpiredUnpaidOrders_WhenSaveFails_RollbacksAndDoesNotThrow()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            RestaurantId = 1,
            OrderDetails = new List<OrderDetail>
            {
                new() { DishId = 11, Quantity = 1, Dish = new Dish { Id = 11, DishName = "D2", Type = DishType.Single } }
            }
        };
        var dbTx = new Mock<IDbTransaction>();

        _mockUnitOfWork.Setup(x => x.Orders.GetExpiredUnpaidOrdersAsync(15)).ReturnsAsync(new List<Order> { order });
        _mockUnitOfWork.Setup(x => x.Transactions.FindAsync(It.IsAny<Expression<Func<Transaction, bool>>>()))
            .ReturnsAsync(new List<Transaction>());
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(dbTx.Object);
        _mockUnitOfWork.Setup(x => x.SaveAsync()).ThrowsAsync(new Exception("save error"));

        var act = async () => await _service.CancelExpiredUnpaidOrdersAsync();

        await act.Should().NotThrowAsync();
        dbTx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmPickupTime_Success_UpdatesAndNotifies()
    {
        var id = Guid.NewGuid();
        var order = new Order { Id = id, RestaurantId = 1, IsPreOrder = true, Status = OrderStatus.Pending };
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(order);

        var result = await _service.ConfirmPickupTimeAsync(new ConfirmPickupTimeRequest
        {
            OrderId = id,
            ConfirmedPickupAt = DateTime.UtcNow.AddMinutes(30)
        });

        result.Should().BeTrue();
        _mockUnitOfWork.Verify(x => x.Orders.Update(order), Times.Once);
        _mockRealtimeService.Verify(x => x.NotifyOrderStatusChanged("1", id.ToString(), (int)OrderStatus.Pending), Times.Once);
    }

    [Fact]
    public async Task ConfirmPickupTime_InvalidOrderId_Throws()
    {
        var act = async () => await _service.ConfirmPickupTimeAsync(new ConfirmPickupTimeRequest
        {
            OrderId = Guid.Empty,
            ConfirmedPickupAt = DateTime.UtcNow.AddMinutes(5)
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.INVALID_ORDER_ID);
    }

    [Fact]
    public async Task ConfirmPickupTime_NotPreOrder_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(new Order { Id = id, IsPreOrder = false });

        var act = async () => await _service.ConfirmPickupTimeAsync(new ConfirmPickupTimeRequest
        {
            OrderId = id,
            ConfirmedPickupAt = DateTime.UtcNow.AddMinutes(10)
        });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ConfirmPickupTime_OrderNotFound_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync((Order)null);

        var act = async () => await _service.ConfirmPickupTimeAsync(new ConfirmPickupTimeRequest
        {
            OrderId = id,
            ConfirmedPickupAt = DateTime.UtcNow.AddMinutes(10)
        });

        await act.Should().ThrowAsync<DomainException>().WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
    }

    [Fact]
    public async Task ConfirmPickupTime_PastTime_Throws()
    {
        var id = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetByIdAsync(id)).ReturnsAsync(new Order { Id = id, IsPreOrder = true });

        var act = async () => await _service.ConfirmPickupTimeAsync(new ConfirmPickupTimeRequest
        {
            OrderId = id,
            ConfirmedPickupAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetTenantOrders_MapsStaffAndOriginalOrderCode()
    {
        var staffId = Guid.NewGuid();
        var originalOrderId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        _mockUnitOfWork.Setup(x => x.Orders.GetTenantOrdersPagedAsync(1, 1, 10, null, null, null, null, null, null))
            .ReturnsAsync((new List<Order>
            {
                new()
                {
                    Id = orderId,
                    OrderCode = 10,
                    RestaurantId = 1,
                    NumberPhone = "0123",
                    TotalAmount = 100,
                    FinalAmount = 90,
                    PromotionDiscount = 10,
                    Status = OrderStatus.Pending,
                    Type = "Cash",
                    typeOrder = TypeOrder.Regular,
                    ResponsibleStaffId = staffId,
                    RefundOrderId = originalOrderId,
                    OrderDetails = new List<OrderDetail>()
                }
            }, 1));

        _mockUnitOfWork.Setup(x => x.Staffs.FindAsync(It.IsAny<Expression<Func<Staff, bool>>>() ))
            .ReturnsAsync(new List<Staff> { new() { Id = staffId, Name = "Cashier A" } });
        _mockUnitOfWork.Setup(x => x.Orders.FindAsync(It.IsAny<Expression<Func<Order, bool>>>() ))
            .ReturnsAsync(new List<Order> { new() { Id = originalOrderId, OrderCode = 99 } });

        var result = await _service.GetTenantOrdersAsync(1, 1, 10);

        result.TotalCount.Should().Be(1);
        result.Items.First().ResponsibleStaffName.Should().Be("Cashier A");
        result.Items.First().OriginalOrderCode.Should().Be(99);
    }

    [Fact]
    public async Task GetTenantOrders_MapsOrderDetailsAndFallbackDishName()
    {
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Orders.GetTenantOrdersPagedAsync(2, 1, 5, null, null, null, null, null, null))
            .ReturnsAsync((new List<Order>
            {
                new()
                {
                    Id = orderId,
                    RestaurantId = 2,
                    OrderCode = 88,
                    NumberPhone = "0999",
                    Status = OrderStatus.Pending,
                    typeOrder = TypeOrder.Regular,
                    OrderDetails = new List<OrderDetail>
                    {
                        new()
                        {
                            DishId = 77,
                            Quantity = 2,
                            SubTotal = 24000,
                            OriginalPrice = 15000,
                            DiscountedPrice = 12000,
                            PromotionAmount = 3000,
                            RefundedQuantity = 1,
                            Dish = null
                        }
                    }
                }
            }, 1));

        var result = await _service.GetTenantOrdersAsync(2, 1, 5);

        result.Items.Should().HaveCount(1);
        result.Items.First().OrderDetails.Should().HaveCount(1);
        result.Items.First().OrderDetails.First().DishName.Should().Be("Unknown");
        result.Items.First().OrderDetails.First().RefundedQuantity.Should().Be(1);
    }

    [Fact]
    public async Task GetTenantOrders_WhenOrderDetailsNull_ReturnsEmptyOrderDetailsList()
    {
        _mockUnitOfWork.Setup(x => x.Orders.GetTenantOrdersPagedAsync(3, 1, 5, null, null, null, null, null, null))
            .ReturnsAsync((new List<Order>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = 3,
                    OrderCode = 300,
                    NumberPhone = "0123",
                    Status = OrderStatus.Pending,
                    typeOrder = TypeOrder.Regular,
                    OrderDetails = null
                }
            }, 1));

        var result = await _service.GetTenantOrdersAsync(3, 1, 5);

        result.Items.Should().HaveCount(1);
        result.Items.First().OrderDetails.Should().NotBeNull();
        result.Items.First().OrderDetails.Should().BeEmpty();
    }

    [Fact]
    public void CalculateDiscountValue_FixedAmount_ReturnsFixedValue()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateDiscountValue", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var promo = new Promotion { DiscountType = DiscountType.FixedAmount, DiscountValue = 7000 };
        var result = (decimal)method!.Invoke(null, new object[] { 50000m, promo })!;

        result.Should().Be(7000);
    }

    [Fact]
    public void CalculateDiscountValue_Percentage_WithMaxCap_ReturnsCappedValue()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateDiscountValue", BindingFlags.NonPublic | BindingFlags.Static);
        var promo = new Promotion
        {
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20,
            MaxDiscountValue = 3000
        };

        var result = (decimal)method!.Invoke(null, new object[] { 50000m, promo })!;

        result.Should().Be(3000);
    }

    [Fact]
    public void CalculateTrueExpiredAt_HappyHour_UsesDailyEndTime()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateTrueExpiredAt", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var now = new DateTime(2026, 4, 18, 10, 0, 0);
        var promo = new Promotion
        {
            Type = PromotionType.HappyHour,
            DailyEndTime = new TimeSpan(12, 30, 0),
            EndDate = now.Date.AddDays(2)
        };

        var result = (DateTime?)method!.Invoke(null, new object[] { promo, now });

        result.Should().Be(now.Date.AddHours(12).AddMinutes(30));
    }

    [Fact]
    public void CalculateTrueExpiredAt_WeeklySpecial_NoDailyEnd_UsesEndOfDay()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateTrueExpiredAt", BindingFlags.NonPublic | BindingFlags.Static);
        var now = new DateTime(2026, 4, 18, 10, 0, 0);
        var promo = new Promotion
        {
            Type = PromotionType.WeeklySpecial,
            EndDate = now.Date.AddDays(5)
        };

        var result = (DateTime?)method!.Invoke(null, new object[] { promo, now });

        result.Should().Be(now.Date.AddDays(1).AddTicks(-1));
    }

    [Fact]
    public void CalculateTrueExpiredAt_Standard_CapsByEndDate()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateTrueExpiredAt", BindingFlags.NonPublic | BindingFlags.Static);
        var now = new DateTime(2026, 4, 18, 10, 0, 0);
        var endDate = now.Date.AddHours(20);
        var promo = new Promotion
        {
            Type = PromotionType.Standard,
            EndDate = endDate
        };

        var result = (DateTime?)method!.Invoke(null, new object[] { promo, now });

        result.Should().Be(endDate);
    }

    [Fact]
    public void CalculateTrueExpiredAt_WhenDailyEndExceedsEndDate_CapsToEndDate()
    {
        var method = typeof(Application.Services.OrderService).GetMethod("CalculateTrueExpiredAt", BindingFlags.NonPublic | BindingFlags.Static);
        var now = new DateTime(2026, 4, 18, 10, 0, 0);
        var promo = new Promotion
        {
            Type = PromotionType.HappyHour,
            DailyEndTime = new TimeSpan(23, 59, 0),
            EndDate = new DateTime(2026, 4, 18, 20, 0, 0)
        };

        var result = (DateTime?)method!.Invoke(null, new object[] { promo, now });

        result.Should().Be(new DateTime(2026, 4, 18, 20, 0, 0));
    }
}
