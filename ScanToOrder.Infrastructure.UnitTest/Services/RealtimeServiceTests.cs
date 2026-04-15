using Microsoft.AspNetCore.SignalR;
using Moq;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Infrastructure.Hubs;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class RealtimeServiceTests
    {
        private readonly Mock<IHubContext<Scan2OrderRealtimeHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly RealtimeService _service;

        public RealtimeServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<Scan2OrderRealtimeHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            _service = new RealtimeService(_mockHubContext.Object);
        }

        #region 1. Tenant Notifications

        [Fact]
        public async Task SendNotificationToTenant_CallsSendAsyncWithCorrectParams()
        {
            // Arrange
            var tenantId = "tenant-1";
            var message = new { Text = "Hello" };

            // Act
            await _service.SendNotificationToTenant(tenantId, message);

            // Assert
            VerifySendAsync("ReceiveNotification", message);
        }

        [Fact]
        public async Task NotifyCountChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyCountChanged("tenant-1", 5);

            // Assert
            VerifySendAsync("CountChanged", 5);
        }

        [Fact]
        public async Task NotifyListChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyListChanged("tenant-1");

            // Assert
            VerifySendAsync("ListChanged");
        }

        [Fact]
        public async Task NotifyTenantProfileChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyTenantProfileChanged("tenant-1");

            // Assert
            VerifySendAsync("ProfileChanged");
        }

        [Fact]
        public async Task NotifySubscriptionChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifySubscriptionChanged("tenant-1");

            // Assert
            VerifySendAsync("SubscriptionChanged");
        }

        #endregion

        #region 2. Order & Kitchen Notifications

        [Fact]
        public async Task SendOrderToKitchen_CallsSendAsync()
        {
            // Arrange
            var order = new OrderRealtimeDto();

            // Act
            await _service.SendOrderToKitchen("res-1", order);

            // Assert
            VerifySendAsync("ReceiveOrder", order);
        }

        [Fact]
        public async Task NotifyOrderCountChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyOrderCountChanged("res-1", 10);

            // Assert
            VerifySendAsync("CountOrderChanged", 10);
        }

        [Fact]
        public async Task NotifyOrderStatusChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyOrderStatusChanged("res-1", "order-1", 2);

            // Assert
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "UpdateStatus",
                It.Is<object[]>(o => o.Length == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task NotifyCustomerOrderStatusChanged_CallsSendAsync_ToCorrectGroup()
        {
            // Act
            await _service.NotifyCustomerOrderStatusChanged("order-123", 3);

            // Assert
            _mockClients.Verify(c => c.Group("order:order-123"), Times.Once);
            VerifySendAsync("CustomerUpdateStatus");
        }

        #endregion

        #region 3. Payment & Shift Notifications

        [Fact]
        public async Task NotifyPaymentReceived_CallsSendAsync()
        {
            // Act
            await _service.NotifyPaymentReceived("res-1", 1234, 50000, "http://audio.url");

            // Assert
            VerifySendAsync("PaymentReceived");
        }

        [Fact]
        public async Task NotifyShiftChanged_CallsSendAsync_ToStaffGroup()
        {
            // Arrange
            var shift = new { Id = 1 };

            // Act
            await _service.NotifyShiftChanged("staff-1", shift);

            // Assert
            _mockClients.Verify(c => c.Group("staff:staff-1"), Times.Once);
            VerifySendAsync("ShiftChanged", shift);
        }

        [Fact]
        public async Task NotifyReceivingOrdersChanged_CallsSendAsync()
        {
            // Act
            await _service.NotifyReceivingOrdersChanged("res-1", true);

            // Assert
            VerifySendAsync("ReceivingOrdersChanged");
        }

        #endregion

        private void VerifySendAsync(string methodName, object expectedArg = null)
        {
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                methodName,
                It.Is<object[]>(o => expectedArg == null || o[0].Equals(expectedArg)),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}