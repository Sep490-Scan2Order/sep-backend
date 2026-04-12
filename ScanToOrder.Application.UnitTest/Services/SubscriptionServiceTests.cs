using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.UnitTest.Services;

public class SubscriptionServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IRestaurantService> _mockRestaurantService;
    private readonly Mock<IDbTransaction> _mockTransaction;
    private readonly SubscriptionService _subscriptionService;

    public SubscriptionServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPaymentService = new Mock<IPaymentService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockEmailService = new Mock<IEmailService>();
        _mockRestaurantService = new Mock<IRestaurantService>();
        _mockTransaction = new Mock<IDbTransaction>();

        _mockRealtimeService.Setup(x => x.NotifySubscriptionChanged(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockRealtimeService.Setup(x => x.NotifyTenantProfileChanged(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _mockTransaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTransaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_mockTransaction.Object);

        _subscriptionService = new SubscriptionService(
            _mockUnitOfWork.Object,
            _mockPaymentService.Object,
            _mockRealtimeService.Object,
            _mockConfiguration.Object,
            _mockEmailService.Object,
            _mockRestaurantService.Object);
    }

    [Fact]
    public async Task ProcessPaymentSuccessAsync_WhenSubscriptionAlreadyMarkedSuccessWithoutLogs_ProcessesSubscription()
    {
        // Arrange
        var transactionCode = 202604120001L;
        var paymentTransaction = new PaymentTransaction
        {
            Id = 7,
            TenantId = Guid.NewGuid(),
            TransactionCode = transactionCode.ToString(),
            PaymentDate = DateTime.UtcNow.AddMinutes(-5),
            TotalAmount = 100000m,
            Status = PaymentTransactionStatus.Success
        };
        paymentTransaction.SetSubscriptionPayload(new List<OrderPayloadItemPlan>
        {
            new()
            {
                RestaurantId = 11,
                NewPlanId = 3,
                ActionType = SubscriptionLogStatus.BuyNew,
                Cycle = BillingCycle.Monthly,
                Quantity = 1,
                AmountAllocated = 100000m,
                BalanceConverted = 0m
            }
        });

        var targetPlan = new Plan
        {
            Id = 3,
            Name = "Pro",
            MonthlyPrice = 100000m,
            YearlyPrice = 1000000m,
            DurationInDays = 30,
            DailyRateMonth = 3333m,
            DailyRateYear = 2778m,
            Level = 2,
            Status = PlanStatus.Active
        };

        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PaymentTransaction, bool>>>(),
                It.IsAny<string>()))
            .ReturnsAsync(paymentTransaction);

        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(
                It.IsAny<Expression<Func<SubscriptionLog, bool>>>() ))
            .ReturnsAsync(false);

        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, Subscription>());
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, Plan> { [targetPlan.Id] = targetPlan });
        _mockUnitOfWork.Setup(x => x.MenuTemplates.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MenuTemplate, bool>>>(),
                It.IsAny<string>()))
            .ReturnsAsync((MenuTemplate?)null);
        _mockUnitOfWork.Setup(x => x.MenuRestaurants.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MenuRestaurant, bool>>>(),
                It.IsAny<string>()))
            .ReturnsAsync((MenuRestaurant?)null);

        _mockUnitOfWork.Setup(x => x.Subscriptions.AddAsync(It.IsAny<Subscription>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.AddAsync(It.IsAny<SubscriptionLog>()))
            .Returns(Task.CompletedTask);

        // Act
        await _subscriptionService.ProcessPaymentSuccessAsync(transactionCode);

        // Assert
        _mockUnitOfWork.Verify(x => x.Subscriptions.AddAsync(It.IsAny<Subscription>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SubscriptionLogs.AddAsync(It.IsAny<SubscriptionLog>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.Is<PaymentTransaction>(pt =>
            pt.Status == PaymentTransactionStatus.Success && pt.TransactionCode == transactionCode.ToString())), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveAsync(), Times.Once);
    }
}