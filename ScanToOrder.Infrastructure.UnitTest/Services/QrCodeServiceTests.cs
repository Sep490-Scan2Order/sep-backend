using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class QrCodeServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly QrCodeService _service;

        public QrCodeServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _service = new QrCodeService(_mockConfiguration.Object);
        }

        #region 1. GenerateRestaurantQrCodeBytes

        [Fact]
        public void GenerateRestaurantQrCodeBytes_ValidSlug_ReturnsNonEmptyByteArray()
        {
            // Arrange
            var slug = "quan-com-tam-dem";
            var fakeBaseUrl = "https://scan2order.io.vn";

            _mockConfiguration.Setup(x => x["FrontEndUrl:scan2order_io_vn"])
                .Returns(fakeBaseUrl);

            // Act
            var result = _service.GenerateRestaurantQrCodeBytes(slug);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        #endregion

        #region 2. GenerateQrCodeBytes

        [Fact]
        public void GenerateQrCodeBytes_ValidContent_ReturnsNonEmptyByteArray()
        {
            // Arrange
            var content = "S2O - He thong goi mon thong minh";

            // Act
            var result = _service.GenerateQrCodeBytes(content);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);
        }

        #endregion
    }
}