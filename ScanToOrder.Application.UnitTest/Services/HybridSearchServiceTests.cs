using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetTopologySuite.Geometries;
using Pgvector;
using ScanToOrder.Application.DTOs.Search;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class HybridSearchServiceTests
    {
        #region 1. Test Fallback
        [Fact]
        public async Task SearchAsync_ApiFails_NoGps_ExecutesKeywordOnly()
        {
            // Arrange
            var mockOpenAi = new Mock<IOpenAiService>();
            var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockProvider = new Mock<IServiceProvider>();
            var mockSearchRepo = new Mock<ISearchRepository>();

            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            mockScope.Setup(x => x.ServiceProvider).Returns(mockProvider.Object);
            mockProvider.Setup(x => x.GetService(typeof(ISearchRepository))).Returns(mockSearchRepo.Object);

            mockOpenAi.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>()))
                      .ThrowsAsync(new Exception("API Down"));

            var catA = new Category { TenantId = Guid.NewGuid() };

            var res1 = new Restaurant { Id = 1, RestaurantName = "R1", Slug = "r1", TenantId = catA.TenantId, IsActive = true };
            var dish1 = new Dish { Id = 1, DishName = "D1", Category = catA };

            mockSearchRepo.Setup(x => x.SearchRestaurantsByKeywordAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Restaurant, double)> { (res1, 0.5) });

            mockSearchRepo.Setup(x => x.SearchDishesByKeywordAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Dish, double)> { (dish1, 0.5) });

            mockUow.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                   .ReturnsAsync(new List<Restaurant> { res1 });

            var request = new HybridSearchRequest { Keyword = "test", TopK = 10 };
            var service = new HybridSearchService(mockOpenAi.Object, mockUow.Object, mockScopeFactory.Object);

            // Act
            var result = await service.SearchAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].RestaurantId.Should().Be(1);

            mockSearchRepo.Verify(x => x.SearchRestaurantsByVectorAsync(It.IsAny<Vector>(), It.IsAny<int>()), Times.Never);
        }
        #endregion

        #region 2. Test Success
        [Fact]
        public async Task SearchAsync_ApiSucceeds_WithGps_CoversAllBranches()
        {
            // Arrange
            var mockOpenAi = new Mock<IOpenAiService>();
            var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockProvider = new Mock<IServiceProvider>();
            var mockSearchRepo = new Mock<ISearchRepository>();

            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);
            mockScope.Setup(x => x.ServiceProvider).Returns(mockProvider.Object);
            mockProvider.Setup(x => x.GetService(typeof(ISearchRepository))).Returns(mockSearchRepo.Object);

            mockOpenAi.Setup(x => x.GetEmbeddingAsync(It.IsAny<string>())).ReturnsAsync(new float[1536]);

            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();
            var tenantZ = Guid.NewGuid();

            var catA = new Category { TenantId = tenantA };
            var catB = new Category { TenantId = tenantB };
            var catZ = new Category { TenantId = tenantZ };

            var res1 = new Restaurant { Id = 1, RestaurantName = "R1", Slug = "r1", TenantId = tenantA, IsActive = true, Location = new Point(10.01, 10.01) };
            var res3 = new Restaurant { Id = 3, RestaurantName = "R3", Slug = "r3", TenantId = tenantB, IsActive = true, Location = new Point(20, 20) };
            var res4 = new Restaurant { Id = 4, RestaurantName = "R4", Slug = "r4", TenantId = tenantB, IsActive = true, Location = null };

            var res5 = new Restaurant { Id = 5, RestaurantName = "R5", Slug = "r5", TenantId = tenantA, IsActive = true };

            var dish1 = new Dish { Id = 1, DishName = "D1", Category = catA };
            var dish3 = new Dish { Id = 3, DishName = "D3", Category = catB };
            var dish4 = new Dish { Id = 4, DishName = "D4", Category = catB };
            var dish99 = new Dish { Id = 99, DishName = "D99", Category = catZ };

            mockSearchRepo.Setup(x => x.SearchRestaurantsByKeywordAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Restaurant, double)> { (res1, 0.1), (res5, 0.2) });

            mockSearchRepo.Setup(x => x.SearchRestaurantsByVectorAsync(It.IsAny<Vector>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Restaurant, double)> {
                    (res1, 0.5),
                    (new Restaurant { Id = 2, Slug = "r2" }, 0.9)
                });

            mockSearchRepo.Setup(x => x.SearchDishesByKeywordAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Dish, double)> { (dish1, 1.5), (dish3, 1.0) });

            mockSearchRepo.Setup(x => x.SearchDishesByVectorAsync(It.IsAny<Vector>(), It.IsAny<int>()))
                .ReturnsAsync(new List<(Dish, double)> {
                    (dish1, 0.1),
                    (new Dish { Id = 2 }, 0.9),
                    (dish4, 0.1),
                    (dish99, 0.1)
                });

            var allRestaurants = new List<Restaurant> { res1, res3, res4 };

            mockUow.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>()))
                   .ReturnsAsync(allRestaurants);

            mockUow.Setup(u => u.Restaurants.GetAllAsync(It.IsAny<Expression<Func<Restaurant, bool>>>(), It.IsAny<Expression<Func<Restaurant, object>>[]>()))
                   .ReturnsAsync(allRestaurants);

            var request = new HybridSearchRequest
            {
                Keyword = "test",
                TopK = 5,
                Latitude = 10.0,
                Longitude = 10.0,
                RadiusKm = 5.0
            };

            var service = new HybridSearchService(mockOpenAi.Object, mockUow.Object, mockScopeFactory.Object);

            // Act
            var result = await service.SearchAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(4); // Có thêm res5

            var res1Result = result.FirstOrDefault(r => r.RestaurantId == 1);
            res1Result.Should().NotBeNull();
            res1Result.SuggestedDishes.Should().ContainSingle(d => d.DishId == 1);

            var res3Result = result.FirstOrDefault(r => r.RestaurantId == 3);
            res3Result.Should().NotBeNull();

            var res4Result = result.FirstOrDefault(r => r.RestaurantId == 4);
            res4Result.Should().NotBeNull();

            var res5Result = result.FirstOrDefault(r => r.RestaurantId == 5);
            res5Result.Should().NotBeNull();
            res5Result.GpsDistanceKm.Should().BeNull(); 

            mockSearchRepo.Verify(x => x.SearchRestaurantsByVectorAsync(It.IsAny<Vector>(), It.IsAny<int>()), Times.Once);
        }
        #endregion
    }
}