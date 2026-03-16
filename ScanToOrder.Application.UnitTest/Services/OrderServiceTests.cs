using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
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
    }

    [Fact]
    public async Task AddToCartAsync_WhenQuantityIsZeroOrLess_ThrowsDomainException()
    {
        #region Arrange

        var request = new AddToCartRequest
        {
            Quantity = 0,
            RestaurantId = 1,
            DishId = 1
        };
        
        #endregion

        #region Act

        var action = async () => await _orderService.AddToCartAsync(request);

        #endregion

        #region Assert

        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OrderMessage.OrderError.QUANTITY_MUST_BE_GREATER_THAN_ZERO);

        #endregion
        
    }
}
