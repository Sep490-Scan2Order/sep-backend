using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using NetTopologySuite.Geometries;
using ScanToOrder.Application.DTOs.Restaurant;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Application.UnitTest.Services;

public partial class RestaurantServiceTests
{
    #region 5. UpdateRestaurantAsync
    [Fact]
    public async Task UpdateRestaurantAsync_WhenNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.UpdateRestaurantAsync(1, Guid.NewGuid(), new UpdateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateRestaurantAsync_WrongTenant_ThrowsException()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "test-slug", TenantId = Guid.NewGuid() };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(restaurant);
        Func<Task> act = async () => await _service.UpdateRestaurantAsync(1, Guid.NewGuid(), new UpdateRestaurantRequest());
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task UpdateRestaurantAsync_InvalidLocation_ThrowsException()
    {
        var tenantId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = 1, Slug = "test-slug", TenantId = tenantId };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1)).ReturnsAsync(restaurant);

        // Missing latitude but has longitude
        var request = new UpdateRestaurantRequest { Longitude = 100 };
        Func<Task> act = async () => await _service.UpdateRestaurantAsync(1, tenantId, request);
        await act.Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.INVALID_RESTAURANT_LOCATION);
        
        // Invalid latitude
        var req2 = new UpdateRestaurantRequest { Latitude = 100.0, Longitude = 20.0 };
        Func<Task> act2 = async () => await _service.UpdateRestaurantAsync(1, tenantId, req2);
        await act2.Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.INVALID_RESTAURANT_LOCATION);
    }
    
    [Fact]
    public async Task UpdateRestaurantAsync_ValidData_Success()
    {
        var tenantId = Guid.NewGuid();
        var restaurant = new Restaurant { Id = 1, TenantId = tenantId, Slug = "test-slug" };
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(restaurant);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new UpdateRestaurantRequest
        {
            RestaurantName = "Updated Name",
            Address = "New Addr",
            Phone = "123",
            Description = "Desc",
            OpenTime = "09:00",
            CloseTime = "10:00",
            Latitude = 10,
            Longitude = 20,
            Image = fileMock.Object
        };
        _mockStorageService.Setup(s => s.UploadFromBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("url2");
        _mockMapper.Setup(m => m.Map<RestaurantDto>(restaurant)).Returns(new RestaurantDto { Id = 1, RestaurantName = "Updated Name" });

        var result = await _service.UpdateRestaurantAsync(1, tenantId, request);
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.Restaurants.Update(restaurant), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    #endregion

    #region 6. GetRestaurantBySlugAsync
    [Fact]
    public async Task GetRestaurantBySlugAsync_WhenNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.GetRestaurantBySlugAsync("slug");
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetRestaurantBySlugAsync_WhenClosedAndNotReceivingAndInactive_ThrowsException()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = false, IsReceivingOrders = false, IsOpened = false };
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(restaurant);
        Func<Task> act = async () => await _service.GetRestaurantBySlugAsync("slug");
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetRestaurantBySlugAsync_WhenValid_ReturnsMappedDto()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = true };
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(restaurant);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(restaurant)).Returns(new RestaurantDto { Id = 1 });
        var result = await _service.GetRestaurantBySlugAsync("slug");
        result.Id.Should().Be(1);
    }
    #endregion

    #region 7. GetRestaurantQrImageBySlugAsync
    [Fact]
    public async Task GetRestaurantQrImageBySlugAsync_WhenNotFound_ThrowsException()
    {
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync((Restaurant?)null);
        Func<Task> act = async () => await _service.GetRestaurantQrImageBySlugAsync("slug");
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetRestaurantQrImageBySlugAsync_WhenInactiveAndClosedAndNotReceiving_ThrowsException()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = false, IsReceivingOrders = false, IsOpened = false };
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(restaurant);
        Func<Task> act = async () => await _service.GetRestaurantQrImageBySlugAsync("slug");
        await act.Should().ThrowAsync<DomainException>().WithMessage(RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
    }
    
    [Fact]
    public async Task GetRestaurantQrImageBySlugAsync_Valid_ReturnsQrBytes()
    {
        var restaurant = new Restaurant { Id = 1, Slug = "slug", IsActive = true };
        _mockUnitOfWork.Setup(u => u.Restaurants.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>(), It.IsAny<string>()))
            .ReturnsAsync(restaurant);
        _mockQrCodeService.Setup(q => q.GenerateRestaurantQrCodeBytes("https://scan2order.id.vn/slug")).Returns(new byte[] { 1, 2, 3 });
        var result = await _service.GetRestaurantQrImageBySlugAsync("slug");
        result.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
    }
    #endregion

    #region 8. GetRestaurantsByTenantIdAsync
    [Fact]
    public async Task GetRestaurantsByTenantIdAsync_ReturnsMappedDtos()
    {
        var restaurants = new List<Restaurant> { new Restaurant { Id = 1, Slug = "slug-1" }, new Restaurant { Id = 2, Slug = "slug-2" } };
        _mockUnitOfWork.Setup(u => u.Restaurants.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Restaurant, bool>>>()))
            .ReturnsAsync(restaurants);
        _mockMapper.Setup(m => m.Map<RestaurantDto>(It.IsAny<Restaurant>())).Returns((Restaurant r) => new RestaurantDto { Id = r.Id });
        var result = await _service.GetRestaurantsByTenantIdAsync(Guid.NewGuid());
        result.Should().HaveCount(2);
    }
    #endregion
}
