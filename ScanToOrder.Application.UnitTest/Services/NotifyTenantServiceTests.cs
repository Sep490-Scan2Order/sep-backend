using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.DTOs.NotifyTenant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Application.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class NotifyTenantServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IRealtimeService> _mockRealtimeService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly NotifyTenantService _service;

        public NotifyTenantServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockRealtimeService = new Mock<IRealtimeService>();
            _mockEmailService = new Mock<IEmailService>();

            _service = new NotifyTenantService(
                _mockUnitOfWork.Object,
                _mockRealtimeService.Object,
                _mockEmailService.Object
            );
        }

        [Fact]
        public async Task CreateNotifyTenantAsync_WithAllValidData_SendsEmailAndNotifications()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new CreateNotifyTenantDtoRequest { NotificationId = 1, TenantIds = new List<Guid> { tenantId } };
            var notification = new Notification
            {
                NotificationId = 1,
                NotifyTitle = "T",
                NotifySub = "S",
                SystemBlogUrl = "http://u.com"
            };
            var mockTenant = new Tenant
            {
                Id = tenantId,
                Name = "Tenant Name",
                Account = new AuthenticationUser { Email = "test@gmail.com" }
            };

            _mockUnitOfWork.Setup(u => u.Notifications.GetByIdAsync(1)).ReturnsAsync(notification);
            _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(mockTenant);

            // Act
            var result = await _service.CreateNotifyTenantAsync(request);

            // Assert
            result.Should().HaveCount(1);
            _mockEmailService.Verify(e => e.SendEmailViaIdDomainAsync("test@gmail.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task CreateNotifyTenantAsync_WhenTenantIsNull_SkipsEmail()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new CreateNotifyTenantDtoRequest { NotificationId = 1, TenantIds = new List<Guid> { tenantId } };
            _mockUnitOfWork.Setup(u => u.Notifications.GetByIdAsync(1)).ReturnsAsync(new Notification { SystemBlogUrl = "u" });

            // Kịch bản 1: currentTenant là null
            _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync((Tenant)null);

            // Act
            await _service.CreateNotifyTenantAsync(request);

            // Assert
            _mockEmailService.Verify(e => e.SendEmailViaIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotifyTenantAsync_WhenAccountIsNull_SkipsEmail()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new CreateNotifyTenantDtoRequest { NotificationId = 1, TenantIds = new List<Guid> { tenantId } };
            _mockUnitOfWork.Setup(u => u.Notifications.GetByIdAsync(1)).ReturnsAsync(new Notification { SystemBlogUrl = "u" });

            // Kịch bản 2: currentTenant không null nhưng Account là null
            var mockTenant = new Tenant { Id = tenantId, Account = null };
            _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(mockTenant);

            // Act
            await _service.CreateNotifyTenantAsync(request);

            // Assert
            _mockEmailService.Verify(e => e.SendEmailViaIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotifyTenantAsync_WhenEmailIsNull_SkipsEmail()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new CreateNotifyTenantDtoRequest { NotificationId = 1, TenantIds = new List<Guid> { tenantId } };
            _mockUnitOfWork.Setup(u => u.Notifications.GetByIdAsync(1)).ReturnsAsync(new Notification { SystemBlogUrl = "u" });

            // Kịch bản 3: Account không null nhưng Email là null
            var mockTenant = new Tenant { Id = tenantId, Account = new AuthenticationUser { Email = null } };
            _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(mockTenant);

            // Act
            await _service.CreateNotifyTenantAsync(request);

            // Assert
            _mockEmailService.Verify(e => e.SendEmailViaIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateNotifyTenantAsync_EmailFails_CatchesException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new CreateNotifyTenantDtoRequest { NotificationId = 1, TenantIds = new List<Guid> { tenantId } };
            var mockTenant = new Tenant { Account = new AuthenticationUser { Email = "e@g.com" } };

            _mockUnitOfWork.Setup(u => u.Notifications.GetByIdAsync(1)).ReturnsAsync(new Notification { SystemBlogUrl = "u" });
            _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(mockTenant);

            _mockEmailService.Setup(e => e.SendEmailViaIdDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("SMTP Error"));

            // Act
            Func<Task> action = async () => await _service.CreateNotifyTenantAsync(request);

            // Assert
            await action.Should().NotThrowAsync();
        }

        [Fact]
        public async Task GetNotifyTenantsAsync_ReturnsList()
        {
            // Arrange
            var list = new List<NotifyTenant> { new NotifyTenant() };
            _mockUnitOfWork.Setup(u => u.NotifyTenants.GetAllAsync(It.IsAny<Expression<Func<NotifyTenant, bool>>>(), It.IsAny<Expression<Func<NotifyTenant, object>>[]>())).ReturnsAsync(list);

            // Act
            var result = await _service.GetNotifyTenantsAsync();

            // Assert
            result.Should().BeEquivalentTo(list);
        }

        [Fact]
        public async Task CountTotalNotifyByTenantId_WithStatus_CallsCount()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.NotifyTenants.CountAsync(It.IsAny<Expression<Func<NotifyTenant, bool>>>())).ReturnsAsync(10);

            // Act
            var result = await _service.CountTotalNotifyByTenantId(Guid.NewGuid(), NotifyTenantStatus.Unread);

            // Assert
            result.Should().Be(10);
        }

        [Fact]
        public async Task CountTotalNotifyByTenantId_NoStatus_CallsCount()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.NotifyTenants.CountAsync(It.IsAny<Expression<Func<NotifyTenant, bool>>>())).ReturnsAsync(20);

            // Act
            var result = await _service.CountTotalNotifyByTenantId(Guid.NewGuid(), null);

            // Assert
            result.Should().Be(20);
        }

        [Fact]
        public async Task UpdateStatusToReadAsync_NoFound_ThrowsException()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.NotifyTenants.FindAsync(It.IsAny<Expression<Func<NotifyTenant, bool>>>())).ReturnsAsync(new List<NotifyTenant>());

            // Act
            Func<Task> action = async () => await _service.UpdateStatusToReadAsync(Guid.NewGuid(), new UpdateNotifyTenantStatusRequestDto { NotificationIds = new List<int> { 1 } });

            // Assert
            await action.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task UpdateStatusToReadAsync_Found_UpdatesStatus()
        {
            // Arrange
            var tid = Guid.NewGuid();
            var nt = new NotifyTenant { NotificationId = 1, TenantId = tid };
            _mockUnitOfWork.Setup(u => u.NotifyTenants.FindAsync(It.IsAny<Expression<Func<NotifyTenant, bool>>>())).ReturnsAsync(new List<NotifyTenant> { nt });

            // Act
            var result = await _service.UpdateStatusToReadAsync(tid, new UpdateNotifyTenantStatusRequestDto { NotificationIds = new List<int> { 1 }, Status = NotifyTenantStatus.Read });

            // Assert
            result.Should().Be(NotifyTenantMessage.NotifyTenantSuccess.ALL_NOTIFY_TENANT_READED);
            nt.Status.Should().Be(NotifyTenantStatus.Read);
        }

        [Fact]
        public async Task GetNotifyDetailsByTenantIdSortBySentAtAsync_ReturnsItems()
        {
            // Arrange
            var notification = new Notification { NotifyTitle = "T", NotifySub = "S", SystemBlogUrl = "U", SentAt = DateTime.UtcNow };
            var list = new List<NotifyTenant> { new NotifyTenant { Notification = notification, Status = NotifyTenantStatus.Unread } };
            _mockUnitOfWork.Setup(u => u.NotifyTenants.GetNotifyDetailsByTenantIdSortBySentAtAsync(1, 10, It.IsAny<Guid>())).ReturnsAsync((list, 1));

            // Act
            var result = await _service.GetNotifyDetailsByTenantIdSortBySentAtAsync(1, 10, Guid.NewGuid());

            // Assert
            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Fact]
        public void DTOs_CheckProperties()
        {
            // Act
            var res = new CreateNotifyTenantDtoResponse { Id = 1, NotificationId = 1, TenantId = Guid.NewGuid(), Status = NotifyTenantStatus.Read };
            var det = new NotifyDetailDtoResponse { NotificationId = 1, NotifyTitle = "T" };

            // Assert
            res.Id.Should().Be(1);
            det.NotifyTitle.Should().Be("T");
        }
    }
}