using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Services;
using System.Linq.Expressions;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class HangfireBackgroundJobServiceTests
    {
        private readonly Mock<IBackgroundJobClient> _mockBackgroundJobClient;
        private readonly HangfireBackgroundJobService _service;

        public HangfireBackgroundJobServiceTests()
        {
            _mockBackgroundJobClient = new Mock<IBackgroundJobClient>();
            _service = new HangfireBackgroundJobService(_mockBackgroundJobClient.Object);
        }

        #region Main Methods Coverage

        [Fact]
        public void EnqueueSearchIndexDish_ShouldEnqueueCorrectJob()
        {
            // Arrange
            var dishId = 123;

            // Act
            _service.EnqueueSearchIndexDish(dishId);

            // Assert
            // Hangfire sử dụng Extension methods, nhưng thực tế nó gọi phương thức Create trên IBackgroundJobClient
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(ISearchIndexService) &&
                    job.Method.Name == nameof(ISearchIndexService.IndexDishAsync) &&
                    (int)job.Args[0] == dishId),
                It.IsAny<IState>()),
            Times.Once);
        }

        [Fact]
        public void EnqueueSearchIndexRestaurant_ShouldEnqueueCorrectJob()
        {
            // Arrange
            var restaurantId = 456;

            // Act
            _service.EnqueueSearchIndexRestaurant(restaurantId);

            // Assert
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(ISearchIndexService) &&
                    job.Method.Name == nameof(ISearchIndexService.IndexRestaurantAsync) &&
                    (int)job.Args[0] == restaurantId),
                It.IsAny<IState>()),
            Times.Once);
        }

        [Fact]
        public void EnqueueFullReIndex_ShouldEnqueueCorrectJob()
        {
            // Act
            _service.EnqueueFullReIndex();

            // Assert
            _mockBackgroundJobClient.Verify(x => x.Create(
                It.Is<Job>(job =>
                    job.Type == typeof(ISearchIndexService) &&
                    job.Method.Name == nameof(ISearchIndexService.FullReIndexAsync)),
                It.IsAny<IState>()),
            Times.Once);
        }

        #endregion
    }
}