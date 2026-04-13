using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
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
        _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
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
        _mockRealtimeService.Setup(x => x.NotifyReceivingOrdersChanged(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        _mockTransaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockTransaction.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(x => x.BeginTransactionAsync())
            .ReturnsAsync(_mockTransaction.Object);
        _mockUnitOfWork.Setup(x => x.SaveAsync())
            .Returns(Task.CompletedTask);

        _subscriptionService = new SubscriptionService(
            _mockUnitOfWork.Object,
            _mockPaymentService.Object,
            _mockRealtimeService.Object,
            _mockConfiguration.Object,
            _mockEmailService.Object,
            _mockRestaurantService.Object);
            
        _mockConfiguration.Setup(x => x["FrontEndUrl:local"]).Returns("");
        _mockConfiguration.Setup(x => x["FrontEndUrl:scan2order_id_vn"]).Returns("http://scan2order.test");
    }

    #region CalculatePreviewAsync
    [Fact]
    public async Task Preview_RestaurantNotFound_SkipsItem()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant>());
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan() } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details.Should().BeEmpty();
        result.TotalAmountToPay.Should().Be(0);
    }

    [Fact]
    public async Task Preview_TargetPlanNotFound_SkipsItem()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan>());
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_NoCurrentSub_BuyNew_MonthlyCycle()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 2 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Name = "Pro", MonthlyPrice = 100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details.Should().HaveCount(1);
        result.TotalAmountToPay.Should().Be(200);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.BuyNew);
    }

    [Fact]
    public async Task Preview_NoCurrentSub_BuyNew_YearlyCycle()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Yearly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Name = "Pro", YearlyPrice = 1000 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.TotalAmountToPay.Should().Be(1000);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.BuyNew);
    }

    [Fact]
    public async Task Preview_WithCurrentSub_Upgrade_OldPlanMonthly_EndDateFuture()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 2, Cycle = BillingCycle.Yearly, Quantity = 1 } } };
        var currentSub = new Subscription { Id = 1, Plan = new Plan { Id = 1, Level = 1, DailyRateMonth = 10 }, StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(10) };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2, Level = 2, Name = "Pro", DailyRateYear = 20, YearlyPrice = 1000 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription> { { 1, currentSub } });

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.Upgrade);
        result.Details[0].BalanceConverted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Preview_WithCurrentSub_Upgrade_OldPlanYearly_EndDatePast()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 2, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        var currentSub = new Subscription { Id = 1, Plan = new Plan { Id = 1, Level = 1, DailyRateYear = 10 }, StartDate = DateTime.UtcNow.AddDays(-400), EndDate = DateTime.UtcNow.AddDays(-35) };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2, Level = 2, Name = "Pro", DailyRateMonth = 20, MonthlyPrice = 100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription> { { 1, currentSub } });

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.Upgrade);
        result.Details[0].BalanceConverted.Should().Be(0);
    }

    [Fact]
    public async Task Preview_WithCurrentSub_Downgrade_OldPlanMonthly()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        var currentSub = new Subscription { Id = 1, Plan = new Plan { Id = 2, Level = 2, DailyRateMonth = 30 }, StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(20) };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, Level = 1, Name = "Free", DailyRateMonth = 10, MonthlyPrice = 50 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription> { { 1, currentSub } });

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.Downgrade);
        result.Details[0].BalanceConverted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Preview_WithCurrentSub_Downgrade_OldPlanYearly_EndDatePast()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Yearly, Quantity = 1 } } };
        var currentSub = new Subscription { Id = 1, Plan = new Plan { Id = 2, Level = 2, DailyRateYear = 30 }, StartDate = DateTime.UtcNow.AddDays(-400), EndDate = DateTime.UtcNow.AddDays(-35) };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, Level = 1, Name = "Free", DailyRateYear = 10, YearlyPrice = 500 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription> { { 1, currentSub } });

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.Downgrade);
        result.Details[0].BalanceConverted.Should().Be(0);
    }

    [Fact]
    public async Task Preview_WithCurrentSub_Renew_SameLevel()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        var currentSub = new Subscription { Id = 1, Plan = new Plan { Id = 1, Level = 1, DailyRateMonth = 10 }, StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(20) };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, Level = 1, Name = "Standard", DailyRateMonth = 10, MonthlyPrice = 100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription> { { 1, currentSub } });

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].ActionType.Should().Be(SubscriptionLogStatus.Renew);
        result.Details[0].BalanceConverted.Should().Be(0);
    }

    [Fact]
    public async Task Preview_AmountToPayLessThanZero_SetsToZero()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, Level = 1, Name = "Standard", MonthlyPrice = -100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        var result = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
        result.Details[0].AmountToPay.Should().Be(0);
        result.TotalAmountToPay.Should().Be(0);
    }
    #endregion

    #region CreatePaymentAsync
    [Fact]
    public async Task CreatePayment_TotalZero_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, MonthlyPrice = 0 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());

        Func<Task> act = async () => await _subscriptionService.CreatePaymentAsync(request, tenantId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreatePayment_Success_ReturnsPaymentLink()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1, RestaurantName = "Test" } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, MonthlyPrice = 100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.AddAsync(It.IsAny<PaymentTransaction>())).Returns(Task.CompletedTask);
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ReturnsAsync("http://payment.test/link");

        var result = await _subscriptionService.CreatePaymentAsync(request, tenantId);
        result.Should().Be("http://payment.test/link");
        _mockTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_PaymentServiceThrows_RollsBackAndThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1 } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, MonthlyPrice = 100 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ThrowsAsync(new Exception("Gateway error"));

        Func<Task> act = async () => await _subscriptionService.CreatePaymentAsync(request, tenantId);
        await act.Should().ThrowAsync<DomainException>();
        _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePayment_LocalBaseUrlConfigured_UsesLocalUrl()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId)).ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test",  Id = 1 } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, MonthlyPrice = 10 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Subscription>());
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.AddAsync(It.IsAny<PaymentTransaction>())).Returns(Task.CompletedTask);
        _mockConfiguration.Setup(x => x["FrontEndUrl:local"]).Returns("http://local.test/");
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).Callback<CreatePaymentRequest>(req => {
                req.ReturnUrl.Should().StartWith("http://local.test/tenant/subscription-callback");
            }).ReturnsAsync("link");

        await _subscriptionService.CreatePaymentAsync(request, tenantId);
    }

    [Fact]
    public async Task CreatePayment_NoBaseUrlConfigured_FallsBackToLocalhost()
    {
        var tenantId = Guid.NewGuid();
        var request = new PlanCheckoutRequest { Items = { new PlanCheckoutItemRequest { RestaurantId = 1, TargetPlanId = 1, Cycle = BillingCycle.Monthly, Quantity = 1 } } };

        _mockUnitOfWork.Setup(x => x.Restaurants.GetByIdsWithTenantId(It.IsAny<List<int>>(), tenantId))
            .ReturnsAsync(new Dictionary<int, Restaurant> { { 1, new Restaurant { Slug = "test", Id = 1 } } });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, MonthlyPrice = 10 } } });
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetByRestaurantIds(It.IsAny<List<int>>()))
            .ReturnsAsync(new Dictionary<int, Subscription>());
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.AddAsync(It.IsAny<PaymentTransaction>()))
            .Returns(Task.CompletedTask);

        _mockConfiguration.Setup(x => x["FrontEndUrl:local"]).Returns("");
        _mockConfiguration.Setup(x => x["FrontEndUrl:scan2order_id_vn"]).Returns("");

        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>()))
            .Callback<CreatePaymentRequest>(req =>
            {
                req.ReturnUrl.Should().StartWith("http://localhost:3000/tenant/subscription-callback/success");
                req.CancelUrl.Should().StartWith("http://localhost:3000/tenant/subscription-callback/cancel");
            })
            .ReturnsAsync("link");

        await _subscriptionService.CreatePaymentAsync(request, tenantId);
    }
    #endregion

    #region CreateCommissionFeePaymentAsync
    [Fact]
    public async Task CreateCommissionFeePayment_TenantNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);
        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateCommissionFeePayment_ConfigurationNull_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations>());
        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateCommissionFeePayment_CommissionRateZero_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 0 } });
        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateCommissionFeePayment_NoDebt_ThrowsDomainException()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TotalDebtAmount = 0 });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 5 } });
        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateCommissionFeePayment_Success_DebtStartedAtHasValue()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TotalDebtAmount = 100.5m, DebtStartedAt = DateTime.UtcNow.AddDays(-10) });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 5 } });
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ReturnsAsync("http://payment.link");

        var result = await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        result.Should().Be("http://payment.link");
        _mockTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateCommissionFeePayment_Success_DebtStartedAtNull()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TotalDebtAmount = 100.5m, DebtStartedAt = null });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 5 } });
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ReturnsAsync("http://payment.link");

        var result = await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        result.Should().Be("http://payment.link");
        _mockTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCommissionFeePayment_DomainExceptionFromPayment_RethrowsAndRollbacks()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TotalDebtAmount = 100m });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 5 } });
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ThrowsAsync(new DomainException("Gateway error"));

        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        await act.Should().ThrowAsync<DomainException>();
        _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCommissionFeePayment_GenericException_WrapsAndRollbacks()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TotalDebtAmount = 100m });
        _mockUnitOfWork.Setup(x => x.Configurations.GetAllAsync(null)).ReturnsAsync(new List<Configurations> { new Configurations { CommissionRate = 5 } });
        _mockPaymentService.Setup(x => x.CreatePaymentLinkAsync(It.IsAny<CreatePaymentRequest>())).ThrowsAsync(new Exception("Unknown error"));

        Func<Task> act = async () => await _subscriptionService.CreateCommissionFeePaymentAsync(tenantId);
        await act.Should().ThrowAsync<DomainException>();
        _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    #endregion

    #region Status Updates
    [Fact]
    public async Task MarkFailed_TransactionNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync((PaymentTransaction?)null);
        Func<Task> act = async () => await _subscriptionService.MarkPaymentFailedAsync(123);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task MarkFailed_AlreadySuccess_ReturnsEarly()
    {
        var pt = new PaymentTransaction { Status = PaymentTransactionStatus.Success };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        await _subscriptionService.MarkPaymentFailedAsync(123);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task MarkFailed_AlreadyCanceled_ReturnsEarly()
    {
        var pt = new PaymentTransaction { Status = PaymentTransactionStatus.Canceled };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        await _subscriptionService.MarkPaymentFailedAsync(123);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task MarkFailed_Pending_UpdatesToFailed()
    {
        var pt = new PaymentTransaction { Status = PaymentTransactionStatus.Pending };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        await _subscriptionService.MarkPaymentFailedAsync(123);
        pt.Status.Should().Be(PaymentTransactionStatus.Failed);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(pt), Times.Once);
    }

    [Fact]
    public async Task MarkCanceled_TransactionNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync((PaymentTransaction?)null);
        Func<Task> act = async () => await _subscriptionService.MarkPaymentCanceledAsync(123, Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task MarkCanceled_WrongTenant_ThrowsDomainException()
    {
        var pt = new PaymentTransaction { TenantId = Guid.NewGuid() };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        Func<Task> act = async () => await _subscriptionService.MarkPaymentCanceledAsync(123, Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task MarkCanceled_AlreadySuccess_ReturnsEarly()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { TenantId = tenantId, Status = PaymentTransactionStatus.Success };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        await _subscriptionService.MarkPaymentCanceledAsync(123, tenantId);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task MarkCanceled_Pending_UpdatesToCanceled()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { TenantId = tenantId, Status = PaymentTransactionStatus.Pending };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        await _subscriptionService.MarkPaymentCanceledAsync(123, tenantId);
        pt.Status.Should().Be(PaymentTransactionStatus.Canceled);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(pt), Times.Once);
    }
    #endregion

    #region GetPaymentStatusAsync
    [Fact]
    public async Task GetStatus_TransactionNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync((PaymentTransaction?)null);
        Func<Task> act = async () => await _subscriptionService.GetPaymentStatusAsync(123, Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetStatus_WrongTenant_ThrowsDomainException()
    {
        var pt = new PaymentTransaction { TenantId = Guid.NewGuid() };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        Func<Task> act = async () => await _subscriptionService.GetPaymentStatusAsync(123, Guid.NewGuid());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetStatus_SubscriptionSuccess_NotProcessed_TriggersProcess()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.Subscription, Status = PaymentTransactionStatus.Success, TransactionCode = "123", Payload = "[]" };
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan>());
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        _mockUnitOfWork.Verify(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>()), Times.AtLeastOnce);
        result.IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_SubscriptionSuccess_AlreadyProcessed_SkipsProcess()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.Subscription, Status = PaymentTransactionStatus.Success, TransactionCode = "123" };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(true);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        result.IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_SubscriptionPending_GatewayPaid_TriggersProcess()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.Subscription, Status = PaymentTransactionStatus.Pending, TransactionCode = "123", Payload = "[]" };
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan>());
        _mockUnitOfWork.SetupSequence(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(pt).ReturnsAsync(pt).ReturnsAsync(pt);
        _mockPaymentService.Setup(x => x.IsPaymentSuccessfulAsync(123)).ReturnsAsync(true);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        _mockPaymentService.Verify(x => x.IsPaymentSuccessfulAsync(123), Times.Once);
        result.IsFinal.Should().BeFalse(); 
    }

    [Fact]
    public async Task GetStatus_SubscriptionPending_GatewayNotPaid_SkipsProcess()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.Subscription, Status = PaymentTransactionStatus.Pending, TransactionCode = "123" };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        _mockPaymentService.Setup(x => x.IsPaymentSuccessfulAsync(123)).ReturnsAsync(false);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        result.IsFinal.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_CommissionFee_SkipsGatewayCheck()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.CommissionFee, Status = PaymentTransactionStatus.Pending, TransactionCode = "123" };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        _mockPaymentService.Verify(x => x.IsPaymentSuccessfulAsync(It.IsAny<long>()), Times.Never);
        result.IsFinal.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_SuccessStatus_IsFinalTrue()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.CommissionFee, Status = PaymentTransactionStatus.Failed, TransactionCode = "123" };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        result.IsFinal.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_PendingStatus_IsFinalFalse()
    {
        var tenantId = Guid.NewGuid();
        var pt = new PaymentTransaction { Id = 1, TenantId = tenantId, PaymentTransactionType = PaymentTransactionType.CommissionFee, Status = PaymentTransactionStatus.Pending, TransactionCode = "123" };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        var result = await _subscriptionService.GetPaymentStatusAsync(123, tenantId);
        result.IsFinal.Should().BeFalse();
    }
    #endregion

    #region ProcessPaymentSuccessAsync & ProcessSubscriptionSuccessAsync
    private PaymentTransaction SetupPt(PaymentTransactionType type, PaymentTransactionStatus status, string payloadStr = "[]")
    {
        var pt = new PaymentTransaction
        {
            Id = 1,
            TenantId = Guid.NewGuid(),
            PaymentTransactionType = type,
            Status = status,
            TransactionCode = "123",
            Payload = payloadStr
        };
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync(pt);
        return pt;
    }

    [Fact]
    public async Task ProcessSuccess_TransactionNotFound_Throws()
    {
        _mockUnitOfWork.Setup(x => x.PaymentTransactions.FirstOrDefaultAsync(It.IsAny<Expression<Func<PaymentTransaction, bool>>>(), It.IsAny<string>())).ReturnsAsync((PaymentTransaction?)null);
        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(1234);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ProcessSuccess_Subscription_AlreadyProcessed_StatusNotSuccess_UpdatesStatus()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(true);
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        pt.Status.Should().Be(PaymentTransactionStatus.Success);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(pt), Times.Once);
    }

    [Fact]
    public async Task ProcessSuccess_Subscription_AlreadyProcessed_StatusSuccess_ReturnsEarly()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Success);
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(true);
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSuccess_CommissionFee_AlreadySuccess_ReturnsEarly()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Success);
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        _mockUnitOfWork.Verify(x => x.PaymentTransactions.Update(It.IsAny<PaymentTransaction>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSuccess_InvalidType_ThrowsDomainException()
    {
        var pt = SetupPt((PaymentTransactionType)999, PaymentTransactionStatus.Pending);
        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(123);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ProcessSubscription_PayloadNull_ReturnsEarly()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending, "");
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        pt.Payload = ""; // make it completely null for property
        
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        _mockUnitOfWork.Verify(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSubscription_PayloadEmpty_ReturnsEarly()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending, "[]");
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        _mockUnitOfWork.Verify(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSubscription_PlanNotFound_ThrowsException()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 99 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription>());
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan>());
        
        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(123);
        await act.Should().ThrowAsync<Exception>().WithMessage("*Target service plan not found*");
    }

    [Fact]
    public async Task ProcessSubscription_BuyNew_NoCurrentSub_Monthly_NoBalance_InsertsNew()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.BuyNew, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription>());
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1 } } });
        _mockUnitOfWork.Setup(x => x.MenuTemplates.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<string>())).ReturnsAsync((MenuTemplate?)null);
        _mockUnitOfWork.Setup(x => x.MenuRestaurants.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>())).ReturnsAsync((MenuRestaurant?)null);

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        _mockUnitOfWork.Verify(x => x.Subscriptions.AddAsync(It.Is<Subscription>(s => s.RestaurantId == 1 && s.PlanId == 1)), Times.Once);
        pt.Status.Should().Be(PaymentTransactionStatus.Success);
    }

    [Fact]
    public async Task ProcessSubscription_BuyNew_ExpiredCurrentSub_ReactivatesExisting()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 2, ActionType = SubscriptionLogStatus.BuyNew, Cycle = BillingCycle.Yearly, Quantity = 1, BalanceConverted = 10 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, Status = SubscriptionStatus.Expired, EndDate = DateTime.UtcNow.AddDays(-10) };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2, DailyRateYear = 5 } } });
        
        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        currentSub.Status.Should().Be(SubscriptionStatus.Active);
        currentSub.PlanId.Should().Be(2);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
    }

    [Fact]
    public async Task ProcessSubscription_Downgrade_UpdatesPlan_ResetsMenuTemplate()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.Downgrade, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 2 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, DailyRateMonth = 10 } } });
        
        var defaultTemplate = new MenuTemplate { Id = 5, IsDefault = true };
        _mockUnitOfWork.Setup(x => x.MenuTemplates.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<string>())).ReturnsAsync(defaultTemplate);
        var menuRes = new MenuRestaurant { RestaurantId = 1, MenuTemplateId = 99 };
        _mockUnitOfWork.Setup(x => x.MenuRestaurants.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>())).ReturnsAsync(menuRes);

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        currentSub.PlanId.Should().Be(1);
        menuRes.MenuTemplateId.Should().Be(5);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
        _mockUnitOfWork.Verify(x => x.MenuRestaurants.Update(menuRes), Times.Once);
    }

    [Fact]
    public async Task ProcessSubscription_Downgrade_MenuRestaurantNull_SkipsMenuReset()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.Downgrade, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 2 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, DailyRateMonth = 10 } } });
        _mockUnitOfWork.Setup(x => x.MenuRestaurants.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>())).ReturnsAsync((MenuRestaurant?)null);

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        _mockUnitOfWork.Verify(x => x.MenuRestaurants.Update(It.IsAny<MenuRestaurant>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSubscription_Downgrade_MenuTemplateAlreadyDefault_SkipsUpdate()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.Downgrade, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 2 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1, DailyRateMonth = 10 } } });
        _mockUnitOfWork.Setup(x => x.MenuTemplates.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<string>())).ReturnsAsync(new MenuTemplate { Id = 12, IsDefault = true });
        var menuRes = new MenuRestaurant { RestaurantId = 1, MenuTemplateId = 12 }; // already default
        _mockUnitOfWork.Setup(x => x.MenuRestaurants.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuRestaurant, bool>>>(), It.IsAny<string>())).ReturnsAsync(menuRes);

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        _mockUnitOfWork.Verify(x => x.MenuRestaurants.Update(It.IsAny<MenuRestaurant>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSubscription_Renew_EndDateFuture_ExtendsFromEndDate()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.Renew, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var futureEndDate = DateTime.UtcNow.AddDays(10);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 1, EndDate = futureEndDate, Status = SubscriptionStatus.Active };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1 } } });

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        (currentSub.EndDate - futureEndDate).TotalDays.Should().BeApproximately(30, 0.1);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
    }

    [Fact]
    public async Task ProcessSubscription_Renew_EndDatePast_ExtendsFromNow()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.Renew, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var pastEndDate = DateTime.UtcNow.AddDays(-10);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 1, EndDate = pastEndDate };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 1, new Plan { Id = 1 } } });

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        (currentSub.EndDate - DateTime.UtcNow).TotalDays.Should().BeApproximately(30, 0.1);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
    }

    [Fact]
    public async Task ProcessSubscription_Upgrade_UpdatesPlan_Monthly()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 2, ActionType = SubscriptionLogStatus.Upgrade, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 1 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2 } } });

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        currentSub.PlanId.Should().Be(2);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
    }

    [Fact]
    public async Task ProcessSubscription_Upgrade_UpdatesPlan_Yearly_WithBalance()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 2, ActionType = SubscriptionLogStatus.Upgrade, Cycle = BillingCycle.Yearly, Quantity = 1, BalanceConverted = 20 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 1 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2, DailyRateYear = 10 } } });

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        currentSub.PlanId.Should().Be(2);
        // Base = 365, Extra = 20/10 = 2 => Total 367
        (currentSub.EndDate - DateTime.UtcNow).TotalDays.Should().BeApproximately(367, 0.1);
        _mockUnitOfWork.Verify(x => x.Subscriptions.Update(currentSub), Times.Once);
    }
    
    [Fact]
    public async Task ProcessSubscription_DailyRateZero_ExtraDaysIsZero()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 2, ActionType = SubscriptionLogStatus.Upgrade, Cycle = BillingCycle.Yearly, Quantity = 1, BalanceConverted = 20 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        var currentSub = new Subscription { RestaurantId = 1, PlanId = 1 };
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { currentSub });
        _mockUnitOfWork.Setup(x => x.Plans.GetByIds(It.IsAny<List<int>>())).ReturnsAsync(new Dictionary<int, Plan> { { 2, new Plan { Id = 2, DailyRateYear = 0 } } }); // Zero rate

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        (currentSub.EndDate - DateTime.UtcNow).TotalDays.Should().BeApproximately(365, 0.1); // No extra days
    }

    [Fact]
    public async Task ProcessSubscription_DBError_RollsBackAndThrowsWrapped()
    {
        var pt = SetupPt(PaymentTransactionType.Subscription, PaymentTransactionStatus.Pending);
        pt.SetSubscriptionPayload(new List<OrderPayloadItemPlan> { new() { RestaurantId = 1, NewPlanId = 1, ActionType = SubscriptionLogStatus.BuyNew, Cycle = BillingCycle.Monthly, Quantity = 1 } });
        _mockUnitOfWork.Setup(x => x.SubscriptionLogs.ExistsAsync(It.IsAny<Expression<Func<SubscriptionLog, bool>>>())).ReturnsAsync(false);
        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ThrowsAsync(new Exception("DB Failure"));

        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(123);
        await act.Should().ThrowAsync<Exception>().WithMessage("*DB Failure*");
        _mockUnitOfWork.Verify(x => x.BeginTransactionAsync(), Times.Never);
    }

    #endregion

    #region ProcessCommissionFeeSuccessAsync
    [Fact]
    public async Task ProcessCommission_PayloadNull_ReturnsEarly()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending, "");
        pt.Payload = ""; // completely null for commission payload
        await _subscriptionService.ProcessPaymentSuccessAsync(123);
        _mockUnitOfWork.Verify(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCommission_TenantNotFound_Throws()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);
        
        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(123);
        await act.Should().ThrowAsync<Exception>().WithMessage("*Tenant không tồn tại*");
    }

    [Fact]
    public async Task ProcessCommission_TenantWasSuspended_ClearsDebtAndUnsuspends()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        var tenant = new Tenant { IsSuspended = true, TotalDebtAmount = 100, SuspendedAt = DateTime.UtcNow };
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);
        _mockUnitOfWork.Setup(x => x.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>(), null)).ReturnsAsync(new List<Restaurant>());

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        tenant.IsSuspended.Should().BeFalse();
        tenant.SuspendedAt.Should().BeNull();
        tenant.TotalDebtAmount.Should().Be(0);
        tenant.DebtStartedAt.Should().BeNull();
        tenant.LastWarningSentAt.Should().BeNull();
        _mockUnitOfWork.Verify(x => x.Tenants.Update(tenant), Times.Once);
    }

    [Fact]
    public async Task ProcessCommission_TenantNotSuspended_ClearsDebt_NoUnsuspend()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        var tenant = new Tenant { IsSuspended = false, TotalDebtAmount = 100 };
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);
        _mockUnitOfWork
            .Setup(x => x.Restaurants.GetAllAsync(
                It.IsAny<Expression<Func<Restaurant, bool>>>(),
                It.IsAny<Expression<Func<Restaurant, object>>[]>()))
            .ReturnsAsync(new List<Restaurant>());

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        tenant.IsSuspended.Should().BeFalse();
        tenant.TotalDebtAmount.Should().Be(0);
        _mockUnitOfWork.Verify(x => x.Tenants.Update(tenant), Times.Once);
    }

    [Fact]
    public async Task ProcessCommission_WithRestaurants_ActivatesAndNotifies()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        var tenant = new Tenant { IsSuspended = false };
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);
        
        var restaurant = new Restaurant { Slug = "test",  Id = 1, IsActive = false, IsReceivingOrders = false };
        _mockUnitOfWork
            .Setup(x => x.Restaurants.GetAllAsync(
                It.IsAny<Expression<Func<Restaurant, bool>>>(),
                It.IsAny<Expression<Func<Restaurant, object>>[]>()))
            .ReturnsAsync(new List<Restaurant> { restaurant });

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        restaurant.IsActive.Should().BeTrue();
        restaurant.IsReceivingOrders.Should().BeTrue();
        _mockUnitOfWork.Verify(x => x.Restaurants.UpdateRange(It.IsAny<IEnumerable<Restaurant>>()), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged("1", true), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyTenantProfileChanged(pt.TenantId.ToString()), Times.Once);
    }

    [Fact]
    public async Task ProcessCommission_NoRestaurants_SkipsRestaurantUpdates()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        var tenant = new Tenant { IsSuspended = false };
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);
        _mockUnitOfWork
            .Setup(x => x.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>(),
                It.IsAny<Expression<Func<Restaurant, object>>[]>()))
            .ReturnsAsync(new List<Restaurant>());

        await _subscriptionService.ProcessPaymentSuccessAsync(123);

        _mockUnitOfWork.Verify(x => x.Restaurants.UpdateRange(It.IsAny<IEnumerable<Restaurant>>()), Times.Once);
        _mockRealtimeService.Verify(r => r.NotifyReceivingOrdersChanged(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCommission_DBError_RollsBackAndWraps()
    {
        var pt = SetupPt(PaymentTransactionType.CommissionFee, PaymentTransactionStatus.Pending);
        pt.SetCommissionPayload(new CommissionFeePayload());
        _mockUnitOfWork.Setup(x => x.Tenants.GetByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception("DB Failure"));

        Func<Task> act = async () => await _subscriptionService.ProcessPaymentSuccessAsync(123);
        await act.Should().ThrowAsync<Exception>().WithMessage("Error while settling commission fee: DB Failure");
        _mockTransaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    #endregion

    #region GetSubscriptionsByTenantAsync
    [Fact]
    public async Task GetSubscriptions_WithActiveSub_MapsDtoCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var restaurants = new List<Restaurant>
        {
            new Restaurant { Slug = "test", 
                Id = 1,
                RestaurantName = "Test",
                Address = "Addr",
                IsActive = true,
                Subscription = new Subscription
                {
                    Id = 10,
                    PlanId = 2,
                    Plan = new Plan { Name = "Pro" },
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                    Status = SubscriptionStatus.Active
                }
            }
        };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId)).ReturnsAsync(restaurants);

        var result = await _subscriptionService.GetSubscriptionsByTenantAsync(tenantId);

        result.Should().HaveCount(1);
        result[0].CurrentSubscriptionId.Should().Be(10);
        result[0].CurrentPlanName.Should().Be("Pro");
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetSubscriptions_WithSubscriptionButPlanNull_CurrentPlanNameIsNull()
    {
        var tenantId = Guid.NewGuid();
        var restaurants = new List<Restaurant>
        {
            new Restaurant
            {
                Slug = "test",
                Id = 1,
                RestaurantName = "Test",
                Address = "Addr",
                IsActive = true,
                Subscription = new Subscription
                {
                    Id = 10,
                    PlanId = 2,
                    Plan = null,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(30),
                    Status = SubscriptionStatus.Active
                }
            }
        };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
            .ReturnsAsync(restaurants);

        var result = await _subscriptionService.GetSubscriptionsByTenantAsync(tenantId);

        result.Should().HaveCount(1);
        result[0].CurrentPlanName.Should().BeNull();
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetSubscriptions_NoSub_MapsDtoWithoutSubInfo()
    {
        var tenantId = Guid.NewGuid();
        var restaurants = new List<Restaurant>
        {
            new Restaurant { Slug = "test", 
                Id = 1,
                RestaurantName = "Test",
                Address = null,
                IsActive = null
            }
        };
        _mockUnitOfWork.Setup(x => x.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId)).ReturnsAsync(restaurants);

        var result = await _subscriptionService.GetSubscriptionsByTenantAsync(tenantId);

        result.Should().HaveCount(1);
        result[0].Address.Should().Be("");
        result[0].IsActive.Should().BeFalse();
        result[0].CurrentSubscriptionId.Should().BeNull();
        result[0].Status.Should().Be("None");
    }

    [Fact]
    public async Task GetSubscriptions_EmptyList_ReturnsEmpty()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(x => x.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId)).ReturnsAsync(new List<Restaurant>());

        var result = await _subscriptionService.GetSubscriptionsByTenantAsync(tenantId);

        result.Should().BeEmpty();
    }
    #endregion

    #region ProcessSubscriptionExpirationsAsync
    [Fact]
    public async Task ProcessExpirations_NoExpiredSubs_SkipsUpdateBlock()
    {
        _mockUnitOfWork.Setup(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        )).ReturnsAsync(new List<Subscription>()); // No expired, no expiring

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockUnitOfWork.Verify(x => x.Subscriptions.UpdateRange(It.IsAny<IEnumerable<Subscription>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpirations_WithExpiredSubs_UpdatesStatusAndDeactivates()
    {
        var expiredSub = new Subscription 
        { 
            Id = 1, 
            Restaurant = new Restaurant { Slug = "test",  Id = 1, Tenant = new Tenant { Account = new AuthenticationUser { Email = "test@test.com" } }, RestaurantName = "R1" },
            EndDate = DateTime.UtcNow.AddDays(-1)
        };
        
        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        ))
        .ReturnsAsync(new List<Subscription> { expiredSub }) // 1. Expired subs
        .ReturnsAsync(new List<Subscription>()); // 2. Expiring subs

        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { expiredSub });
        _mockUnitOfWork.Setup(x => x.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(new List<Restaurant> { expiredSub.Restaurant });

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        expiredSub.Status.Should().Be(SubscriptionStatus.Expired);
        expiredSub.Restaurant.IsActive.Should().BeFalse();
        expiredSub.Restaurant.IsOpened.Should().BeFalse();
        _mockUnitOfWork.Verify(x => x.Subscriptions.UpdateRange(It.IsAny<IEnumerable<Subscription>>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.Restaurants.UpdateRange(It.IsAny<IEnumerable<Restaurant>>()), Times.Once);
        _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync("test@test.com", "Thông báo: Gói dịch vụ đã hết hạn - ScanToOrder", It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpirations_ExpiredSubs_NoEmail_SkipsEmail()
    {
        var expiredSub = new Subscription 
        { 
            Id = 1, 
            Restaurant = new Restaurant { Slug = "test",  Id = 1, Tenant = new Tenant { Account = new AuthenticationUser { Email = "" } } } // no email
        };
        
        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        )).ReturnsAsync(new List<Subscription> { expiredSub }).ReturnsAsync(new List<Subscription>());

        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription>());
        _mockUnitOfWork.Setup(x => x.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(new List<Restaurant>());

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpirations_WithExpiringSubs_SendsWarningEmails()
    {
        var expiringSub = new Subscription 
        { 
            Id = 1, 
            Restaurant = new Restaurant { Slug = "test",  Id = 1, Tenant = new Tenant { Account = new AuthenticationUser { Email = "warn@test.com" } }, RestaurantName = "R2" },
            EndDate = DateTime.UtcNow.AddHours(12)
        };
        
        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        ))
        .ReturnsAsync(new List<Subscription>()) // 1. Expired subs
        .ReturnsAsync(new List<Subscription> { expiringSub }); // 2. Expiring subs

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync("warn@test.com", "Sắp hết hạn gói dịch vụ - Vui lòng gia hạn", It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpirations_NoExpiringSubs_SkipsWarningEmails()
    {
        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        )).ReturnsAsync(new List<Subscription>()).ReturnsAsync(new List<Subscription>());

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpirations_ExpiringSubs_EmailMissing_FiltersOutAndSkipsWarningEmails()
    {
        var expiringSub = new Subscription
        {
            Id = 1,
            Restaurant = new Restaurant
            {
                Slug = "test",
                Id = 1,
                RestaurantName = "R2",
                Tenant = new Tenant
                {
                    Account = new AuthenticationUser { Email = "" }
                }
            },
            EndDate = DateTime.UtcNow.AddHours(12)
        };

        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
                It.IsAny<Expression<Func<Subscription, bool>>>(),
                It.IsAny<Expression<Func<Subscription, object>>>(),
                It.IsAny<Expression<Func<Subscription, object>>>(),
                It.IsAny<Expression<Func<Subscription, object>>>()
            ))
            .ReturnsAsync(new List<Subscription>()) // 1. Expired subs
            .ReturnsAsync(new List<Subscription> { expiringSub }); // 2. Expiring subs

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockEmailService.Verify(
            e => e.SendEmailWithTemplateIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessExpirations_NoRestaurantOnSub_FiltersCorrectly()
    {
        var expiredSub = new Subscription { Id = 1, Restaurant = null! }; // boundary condition
        
        _mockUnitOfWork.SetupSequence(x => x.Subscriptions.GetAllAsync(
            It.IsAny<Expression<Func<Subscription, bool>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>(),
            It.IsAny<Expression<Func<Subscription, object>>>()
        )).ReturnsAsync(new List<Subscription> { expiredSub }).ReturnsAsync(new List<Subscription>());

        _mockUnitOfWork.Setup(x => x.Subscriptions.FindAsync(It.IsAny<Expression<Func<Subscription, bool>>>())).ReturnsAsync(new List<Subscription> { expiredSub });
        _mockUnitOfWork.Setup(x => x.Restaurants.FindAsync(It.IsAny<Expression<Func<Restaurant, bool>>>())).ReturnsAsync(new List<Restaurant>());

        await _subscriptionService.ProcessSubscriptionExpirationsAsync();

        _mockEmailService.Verify(e => e.SendEmailWithTemplateIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
    #endregion
}


