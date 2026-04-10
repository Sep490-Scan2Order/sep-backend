using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _service = new NotificationService(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task CreateNotificationAsync_ValidRequest_CreatesAndReturnsDto()
        {
            // Arrange
            var request = new CreateNotificationDtoRequest
            {
                NotifyTitle = "Khuyến mãi Tết",
                NotifySub = "Giảm giá 50% toàn menu",
                SystemBlogUrl = "https://example.com/blog/tet"
            };

            _mockUnitOfWork.Setup(u => u.Notifications.AddAsync(It.IsAny<Notification>()))
                .Callback<Notification>(n =>
                {
                    n.NotificationId = 99;
                    n.CreatedAt = new DateTime(2026, 4, 10);
                })
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.CreateNotificationAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(99);
            result.NotifyTitle.Should().Be("Khuyến mãi Tết");
            result.NotifySub.Should().Be("Giảm giá 50% toàn menu");
            result.SystemBlogUrl.Should().Be("https://example.com/blog/tet");
            result.SentAt.Should().Be(new DateTime(2026, 4, 10));

            _mockUnitOfWork.Verify(u => u.Notifications.AddAsync(It.IsAny<Notification>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task GetNotificationsAsync_ValidPagination_ReturnsMappedDtoAndCount()
        {
            // Arrange
            int pageIndex = 1;
            int pageSize = 10;
            int expectedTotalCount = 50;

            var mockItems = new List<Notification>
            {
                new Notification
                {
                    NotificationId = 1,
                    NotifyTitle = "Title 1",
                    NotifySub = "Sub 1",
                    SentAt = new DateTime(2026, 1, 1)
                },
                new Notification
                {
                    NotificationId = 2,
                    NotifyTitle = "Title 2",
                    NotifySub = "Sub 2",
                    SentAt = new DateTime(2026, 1, 2)
                }
            };

            _mockUnitOfWork.Setup(u => u.Notifications.GetNotificationSortBySentAtAsync(pageIndex, pageSize))
                .ReturnsAsync((mockItems, expectedTotalCount));

            // Act
            var result = await _service.GetNotificationsAsync(pageIndex, pageSize);

            // Assert
            result.Items.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.TotalCount.Should().Be(50);

            result.Items[0].NotificationId.Should().Be(1);
            result.Items[0].NotifyTitle.Should().Be("Title 1");
            result.Items[0].NotifySub.Should().Be("Sub 1");
            result.Items[0].SentAt.Should().Be(new DateTime(2026, 1, 1));

            result.Items[1].NotificationId.Should().Be(2);

            _mockUnitOfWork.Verify(u => u.Notifications.GetNotificationSortBySentAtAsync(pageIndex, pageSize), Times.Once);
        }
    }
}