using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using NetTopologySuite.Geometries;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.DTOs.Restaurant.Report;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Application.UnitTest.Services;

public partial class RestaurantServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IMapper> _mockMapper;
    private readonly Mock<IQrCodeService> _mockQrCodeService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IDishRedisService> _mockDishRedisService;
    private readonly Mock<IPlanLimitationService> _mockPlanLimitationService;
    private readonly Mock<IMenuCacheService> _mockMenuCacheService;
    private readonly Mock<IBackgroundJobService> _mockBackgroundJobService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;

    private readonly RestaurantService _service;

    public RestaurantServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockMapper = new Mock<IMapper>();
        _mockQrCodeService = new Mock<IQrCodeService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockStorageService = new Mock<IStorageService>();
        _mockDishRedisService = new Mock<IDishRedisService>();
        _mockPlanLimitationService = new Mock<IPlanLimitationService>();
        _mockMenuCacheService = new Mock<IMenuCacheService>();
        _mockBackgroundJobService = new Mock<IBackgroundJobService>();
        _mockRealtimeService = new Mock<IRealtimeService>();

        _service = new RestaurantService(
            _mockUnitOfWork.Object,
            _mockMapper.Object,
            _mockQrCodeService.Object,
            _mockConfiguration.Object,
            _mockStorageService.Object,
            _mockDishRedisService.Object,
            _mockPlanLimitationService.Object,
            _mockMenuCacheService.Object,
            _mockBackgroundJobService.Object,
            _mockRealtimeService.Object
        );
    }

    #region 1. GetRestaurantByIdAsync
    [Fact]
    public async Task GetRestaurantByIdAsync_WhenNotFound_ReturnsNull()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant?)null);
        var result = await _service.GetRestaurantByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRestaurantByIdAsync_WhenExistsAndInactive_StillReturnsMappedDto()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = false, IsReceivingOrders = false, IsOpened = false };
        var dto = new RestaurantDto { Id = 1 };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(restaurant)).Returns(dto);
        var result = await _service.GetRestaurantByIdAsync(1);
        result.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task GetRestaurantByIdAsync_WhenValid_ReturnsMappedDto()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = true, IsReceivingOrders = false, IsOpened = false };
        var dto = new RestaurantDto { Id = 1 };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(restaurant)).Returns(dto);
        var result = await _service.GetRestaurantByIdAsync(1);
        result.Should().BeEquivalentTo(dto);
    }
    #endregion

    #region 2. GetRestaurantsPagedAsync
    [Fact]
    public async Task GetRestaurantsPagedAsync_WithCoordinates_ReturnsOrderedByDistance()
    {
        var restaurantsByDist = new List<(Restaurant Restaurant, double DistanceKm)> { (new Restaurant { Id = 1, Slug = "slug" }, 2.5) };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetRestaurantsSortedByDistancePagedAsync(10, 20, 1, 20, null))
                       .ReturnsAsync((restaurantsByDist, 1));
        _mockMapper.Setup(m => m.Map<RestaurantDto>(It.IsAny<Restaurant>())).Returns(new RestaurantDto { Id = 1 });
        var result = await _service.GetRestaurantsPagedAsync(10, 20, 0, 0, "  ");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRestaurantsPagedAsync_WithoutCoordinates_ReturnsOrderedByTotalOrder()
    {
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug" } };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetRestaurantsSortedByTotalOrderPagedAsync(1, 20, "key"))
                       .ReturnsAsync((restaurants, 1));
        var dtoList = new List<RestaurantDto> { new RestaurantDto { Id = 1 } };
        _mockMapper.Setup(m => m.Map<List<RestaurantDto>>(restaurants)).Returns(dtoList);
        var result = await _service.GetRestaurantsPagedAsync(null, null, 1, 20, "key");
        result.TotalCount.Should().Be(1);
    }
    #endregion

    #region 3. GetNearbyRestaurantsAsync
    [Fact]
    public async Task GetNearbyRestaurantsAsync_ReturnsMappedDtosWithDistance()
    {
        var rawResult = new List<(Restaurant Restaurant, double DistanceKm)> { (new Restaurant { Id = 1, Slug = "slug" }, 1.5) };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetNearbyRestaurantsAsync(10, 20, 5, 10)).ReturnsAsync(rawResult);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(It.IsAny<Restaurant>())).Returns(new RestaurantDto { Id = 1 });
        var result = await _service.GetNearbyRestaurantsAsync(10, 20, 5, 10);
        result.Should().HaveCount(1);
    }
    #endregion

    #region 4. CreateRestaurantAsync
    [Fact]
    public async Task CreateRestaurantAsync_MissingTenant_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant?)null);
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Theory]
    [InlineData(100.0, 20.0)]
    [InlineData(20.0, 200.0)]
    [InlineData(20.0, null)]
    public async Task CreateRestaurantAsync_InvalidLocation_ThrowsException(double? lat, double? lon)
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant { Id = Guid.NewGuid() });
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest { Latitude = lat, Longitude = lon });
        await act.Should().ThrowAsync<DomainException>();
    }

    
    [Fact]
    public async Task CreateRestaurantAsync_MissingTaxNumber_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant { Id = Guid.NewGuid(), BankId = Guid.NewGuid(), CardNumber = "123" });
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_MISSING_TAX_NUMBER);
    }

    [Fact]
    public async Task CreateRestaurantAsync_MissingBankId_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant { Id = Guid.NewGuid(), TaxNumber = "TAX", CardNumber = "123" });
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_MISSING_BANK);
    }
    
    [Fact]
    public async Task CreateRestaurantAsync_MissingCardNumber_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant { Id = Guid.NewGuid(), TaxNumber = "TAX", BankId = Guid.NewGuid() });
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_MISSING_CARD);
    }

    [Fact]
    public async Task CreateRestaurantAsync_MissingPhone_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Tenant { Id = Guid.NewGuid(), TaxNumber = "TAX", BankId = Guid.NewGuid(), CardNumber = "123" });
        Func<Task> act = async () => await _service.CreateRestaurantAsync(Guid.NewGuid(), new CreateRestaurantRequest()); // Phone is null
        await act.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_MISSING_PHONE);
    }

    [Fact]
    public async Task CreateRestaurantAsync_TimeOnlyParsingFallback_Success()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TaxNumber = "TAX", BankId = Guid.NewGuid(), CardNumber = "CARD" });

        var request = new CreateRestaurantRequest { RestaurantName = "Test", Phone = "012", OpenTime = "InvalidTime", CloseTime = "InvalidTime" }; // Image is null
        var mappedRestaurant = new Restaurant { Id = 1, Slug = "test" };
        
        _mockBackgroundJobService.Setup(b => b.EnqueueSearchIndexRestaurant(1));
        _mockMapper.Setup(m => m.Map<Restaurant>(request)).Returns(mappedRestaurant);
        _mockConfiguration.Setup(c => c["FrontEndUrl:scan2order_id_vn"]).Returns("http://test.com");
        _mockQrCodeService.Setup(q => q.GenerateRestaurantQrCodeBytes(It.IsAny<string>())).Returns(new byte[] { 1 });
        _mockUnitOfWork.Setup(u => u.MenuTemplates.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<string>())).ReturnsAsync((MenuTemplate?)null);
        _mockUnitOfWork.Setup(u => u.Restaurants.AddAsync(It.IsAny<Restaurant>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.MenuRestaurants.AddAsync(It.IsAny<MenuRestaurant>())).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(mappedRestaurant)).Returns(new RestaurantDto { Id = 1 });

        var result = await _service.CreateRestaurantAsync(tenantId, request);
        result.Should().NotBeNull();
        mappedRestaurant.OpenTime.Should().BeNull();
        mappedRestaurant.CloseTime.Should().BeNull();
    }

    [Fact]
    public async Task CreateRestaurantAsync_ValidData_Success()
    {
        var tenantId = Guid.NewGuid();
        _mockUnitOfWork.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId, TaxNumber = "TAX", BankId = Guid.NewGuid(), CardNumber = "CARD" });
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateRestaurantRequest { RestaurantName = "Test Res #1", Phone = "0123456789", OpenTime = "08:00", CloseTime = "22:00", Latitude = 10.0, Longitude = 20.0, Image = fileMock.Object };
        var mappedRestaurant = new Restaurant { Id = 1, Slug = "test-res-1" };
        
        _mockBackgroundJobService.Setup(b => b.EnqueueSearchIndexRestaurant(1));
        _mockMapper.Setup(m => m.Map<Restaurant>(request)).Returns(mappedRestaurant);
        _mockConfiguration.Setup(c => c["FrontEndUrl:scan2order_id_vn"]).Returns("http://test.com");
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("url");
        _mockQrCodeService.Setup(q => q.GenerateRestaurantQrCodeBytes(It.IsAny<string>())).Returns(new byte[] { 1 });
        var defaultTemplate = new MenuTemplate() { Id = 1 };
        _mockUnitOfWork.Setup(u => u.MenuTemplates.FirstOrDefaultAsync(It.IsAny<Expression<Func<MenuTemplate, bool>>>(), It.IsAny<string>())).ReturnsAsync(defaultTemplate);
        _mockUnitOfWork.Setup(u => u.Restaurants.AddAsync(It.IsAny<Restaurant>())).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.MenuRestaurants.AddAsync(It.IsAny<MenuRestaurant>())).Returns(Task.CompletedTask);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(mappedRestaurant)).Returns(new RestaurantDto { Id = 1 });

        var result = await _service.CreateRestaurantAsync(tenantId, request);
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Exactly(2));
    }
    #endregion
}


