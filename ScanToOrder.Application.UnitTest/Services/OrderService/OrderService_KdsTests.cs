using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using AutoMapper;

namespace ScanToOrder.Application.UnitTest.Services;

public class OrderService_KdsTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ICartRedisService> _mockCartRedisService;
    private readonly Mock<ITransactionRedisService> _mockTransactionRedisService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IMenuCacheService> _mockMenuCacheService;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IAuthenticatedUserService> _mockAuthUserService;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<ScanToOrder.Application.Services.OrderService>> _mockLogger;
    private readonly Mock<IQrCodeService> _mockQrCodeService;
    private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
    private readonly Mock<IAIUpsellService> _mockAiUpsellService;

    private readonly ScanToOrder.Application.Services.OrderService _orderService;

    public OrderService_KdsTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCartRedisService = new Mock<ICartRedisService>();
        _mockTransactionRedisService = new Mock<ITransactionRedisService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockMenuCacheService = new Mock<IMenuCacheService>();
        _mockMapper = new Mock<IMapper>();
        _mockAuthUserService = new Mock<IAuthenticatedUserService>();
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<ScanToOrder.Application.Services.OrderService>>();
        _mockQrCodeService = new Mock<IQrCodeService>();
        _mockPlanLimitationService = new Mock<IPlanLimitationService>();
        _mockAiUpsellService = new Mock<IAIUpsellService>();

        _orderService = new ScanToOrder.Application.Services.OrderService(
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

    #region GetKdsActiveOrders

    [Fact]
    public async Task GetKdsActiveOrders_WhenRestaurantNotFound_ThrowsDomainException()
    {
        // Arrange
        int restaurantId = 1;
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(restaurantId))
            .ReturnsAsync((Restaurant)null);

        // Act
        var action = async () => await _orderService.GetKdsActiveOrders(restaurantId);

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }

    [Fact]
    public async Task GetKdsActiveOrders_WhenNoOrdersFound_ReturnsEmptyList()
    {
        // Arrange
        int restaurantId = 1;
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(restaurantId))
            .ReturnsAsync(new Restaurant { Id = restaurantId, Slug = "test-restaurant" });
        _mockUnitOfWork.Setup(u => u.Orders.GetOrdersForKdsAsync(restaurantId))
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await _orderService.GetKdsActiveOrders(restaurantId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetKdsActiveOrders_WhenOrdersExist_ReturnsMappedOrders()
    {
        // Arrange
        int restaurantId = 1;
        var orders = new List<Order>
        {
            new Order 
            { 
                Id = Guid.NewGuid(), 
                OrderCode = 101, 
                Status = OrderStatus.Pending,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { Id = 1, Dish = new ScanToOrder.Domain.Entities.Dishes.Dish { DishName = "Burger" }, Quantity = 1 }
                }
            }
        };

        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(restaurantId))
            .ReturnsAsync(new Restaurant { Id = restaurantId, Slug = "test-restaurant" });
        _mockUnitOfWork.Setup(u => u.Orders.GetOrdersForKdsAsync(restaurantId))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetKdsActiveOrders(restaurantId);

        // Assert
        result.Should().HaveCount(1);
        result[0].OrderCode.Should().Be(101);
        result[0].Items.Should().HaveCount(1);
        result[0].Items[0].Name.Should().Be("Burger");
    }

    [Fact]
    public async Task GetKdsActiveOrders_WhenRefundOrdersExist_MapsOriginalOrderCode()
    {
        // Arrange
        int restaurantId = 1;
        var originalOrderId = Guid.NewGuid();
        var refundOrderId = Guid.NewGuid();
        
        var orders = new List<Order>
        {
            new Order 
            { 
                Id = refundOrderId, 
                OrderCode = 202, 
                Status = OrderStatus.Pending,
                RefundOrderId = originalOrderId,
                OrderDetails = new List<OrderDetail>()
            }
        };

        var originalOrders = new List<Order>
        {
            new Order { Id = originalOrderId, OrderCode = 101 }
        };

        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(restaurantId))
            .ReturnsAsync(new Restaurant { Id = restaurantId, Slug = "test-restaurant" });
        _mockUnitOfWork.Setup(u => u.Orders.GetOrdersForKdsAsync(restaurantId))
            .ReturnsAsync(orders);
        _mockUnitOfWork.Setup(u => u.Orders.FindAsync(It.IsAny<Expression<Func<Order, bool>>>()))
            .ReturnsAsync(originalOrders);

        // Act
        var result = await _orderService.GetKdsActiveOrders(restaurantId);

        // Assert
        result.Should().HaveCount(1);
        result[0].OriginalOrderCode.Should().Be(101);
    }

    #endregion

    #region UpdateOrderStatus

    [Fact]
    public async Task UpdateOrderStatus_WhenOrderNotFound_ThrowsDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync((Order)null);

        // Act
        var action = async () => await _orderService.UpdateOrderStatus(orderId, OrderStatus.Preparing);

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OrderMessage.OrderError.ORDER_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenInvalidStatusTransition_ThrowsDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, Status = OrderStatus.Ready };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var action = async () => await _orderService.UpdateOrderStatus(orderId, OrderStatus.Preparing);

        // Assert
        await action.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenPreOrderNotConfirmed_ThrowsDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, Status = OrderStatus.Pending, IsPreOrder = true, ConfirmedPickupAt = null };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var action = async () => await _orderService.UpdateOrderStatus(orderId, OrderStatus.Preparing);

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Đơn hàng đặt trước cần được xác nhận thời gian nhận hàng trước khi chế biến.");
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenOrderAlreadyServed_ThrowsDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, Status = OrderStatus.Served };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var action = async () => await _orderService.UpdateOrderStatus(orderId, OrderStatus.Cancelled);

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage("Đơn hàng đã hoàn thành (Served), không thể cập nhật thêm.");
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenSuccessful_UpdatesStatusAndNotifies()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var restaurantId = 123;
        var order = new Order { Id = orderId, Status = OrderStatus.Pending, RestaurantId = restaurantId };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await _orderService.UpdateOrderStatus(orderId, OrderStatus.Preparing);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Preparing);
        _mockUnitOfWork.Verify(u => u.Orders.Update(order), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyOrderStatusChanged(restaurantId.ToString(), orderId.ToString(), (int)OrderStatus.Preparing), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_WhenCancelled_SucceedsRegardlessOfSequence()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, Status = OrderStatus.Ready, RestaurantId = 1 };
        _mockUnitOfWork.Setup(u => u.Orders.GetByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await _orderService.UpdateOrderStatus(orderId, OrderStatus.Cancelled);

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    #endregion
}
