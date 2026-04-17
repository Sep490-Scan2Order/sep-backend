using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class HuggingFaceServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly Mock<IOptions<AiSettings>> _mockOptions;
        private readonly AiSettings _settings;

        public HuggingFaceServiceTests()
        {
            _mockHandler = new Mock<HttpMessageHandler>();
            _mockOptions = new Mock<IOptions<AiSettings>>();

            _settings = new AiSettings { HuggingFaceApiKey = "fake-key" };
            _mockOptions.Setup(x => x.Value).Returns(_settings);
        }

        #region 1. Constructor & Configuration Tests

        [Fact]
        public void Constructor_WhenApiKeyIsMissing_ShouldThrowArgumentNullException()
        {
            // Arrange
            _settings.HuggingFaceApiKey = null;
            var httpClient = new HttpClient(_mockHandler.Object);

            // Act
            Action act = () => new HuggingFaceService(httpClient, _mockOptions.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("HuggingFaceApiKey")
               .And.Message.Should().Contain("is missing in configuration");
        }

        #endregion

        #region 2. GenerateImageBytesAsync Coverage

        [Fact]
        public async Task GenerateImageBytesAsync_WhenSuccessful_ShouldReturnByteArray()
        {
            // Arrange
            var expectedBytes = Encoding.UTF8.GetBytes("fake-image-data");
            var httpClient = new HttpClient(_mockHandler.Object);
            var service = new HuggingFaceService(httpClient, _mockOptions.Object);

            // Mock Response từ HuggingFace
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(expectedBytes)
                });

            // Act
            var result = await service.GenerateImageBytesAsync("A beautiful restaurant");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedBytes);

            // Verify request content & Auth header
            _mockHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.Headers.Authorization!.Scheme == "Bearer" &&
                    req.Headers.Authorization!.Parameter == "fake-key" &&
                    req.RequestUri!.ToString().Contains("stabilityai/stable-diffusion-xl-base-1.0")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task GenerateImageBytesAsync_WhenApiFails_ShouldThrowExceptionWithDetails()
        {
            // Arrange
            var errorContent = "Model is loading";
            var httpClient = new HttpClient(_mockHandler.Object);
            var service = new HuggingFaceService(httpClient, _mockOptions.Object);

            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent(errorContent)
                });

            // Act
            Func<Task> act = async () => await service.GenerateImageBytesAsync("prompt");

            // Assert
            await act.Should().ThrowAsync<Exception>()
                .WithMessage("*Hugging Face API Error (ServiceUnavailable): Model is loading*");
        }

        #endregion
    }
}