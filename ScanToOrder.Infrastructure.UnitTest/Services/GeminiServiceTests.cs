using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;
using System.Net;
using System.Text.Json;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class GeminiServiceTests
    {
        private readonly Mock<IOptions<AiSettings>> _mockOptions;
        private readonly LocalFakeHttpMessageHandler _fakeHandler;
        private readonly HttpClient _httpClient;

        public GeminiServiceTests()
        {
            _mockOptions = new Mock<IOptions<AiSettings>>();
            _fakeHandler = new LocalFakeHttpMessageHandler();
            _httpClient = new HttpClient(_fakeHandler);
        }

        #region 1. Constructor Branches (Null Checks)

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenGeminiKeyIsMissing_ThrowsArgumentNullException(string key)
        {
            // Arrange
            var settings = new AiSettings { GeminiKey = key, GeminiModel = "gemini-pro" };
            _mockOptions.Setup(o => o.Value).Returns(settings);

            // Act
            Action act = () => new GeminiService(_httpClient, _mockOptions.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("GeminiKey");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenGeminiModelIsMissing_ThrowsArgumentNullException(string model)
        {
            // Arrange
            var settings = new AiSettings { GeminiKey = "valid-key", GeminiModel = model };
            _mockOptions.Setup(o => o.Value).Returns(settings);

            // Act
            Action act = () => new GeminiService(_httpClient, _mockOptions.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("GeminiModel");
        }

        #endregion

        #region 2. GenerateHolidayVisualConfigAsync Tests

        [Fact]
        public async Task GenerateHolidayVisualConfigAsync_Success_ReturnsDto()
        {
            // Arrange
            var settings = new AiSettings { GeminiKey = "key", GeminiModel = "model" };
            _mockOptions.Setup(o => o.Value).Returns(settings);
            var service = new GeminiService(_httpClient, _mockOptions.Object);

            var innerDto = new AiHolidayVisualDto
            {
                TemplateName = "Tết",
                ThemeColor = "#FF0000",
                BackgroundImagePrompt = "A beautiful Tet background"
            };
            var innerJson = JsonSerializer.Serialize(innerDto);

            var geminiResponse = new
            {
                candidates = new[]
                {
                    new
                    {
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = innerJson }
                            }
                        }
                    }
                }
            };

            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(geminiResponse))
            };

            // Act
            var result = await service.GenerateHolidayVisualConfigAsync("Tết Nguyên Đán");

            // Assert
            result.Should().NotBeNull();
            result.TemplateName.Should().Be("Tết");
            result.ThemeColor.Should().Be("#FF0000");
        }

        [Fact]
        public async Task GenerateHolidayVisualConfigAsync_ApiFailure_ThrowsHttpRequestException()
        {
            // Arrange
            var settings = new AiSettings { GeminiKey = "key", GeminiModel = "model" };
            _mockOptions.Setup(o => o.Value).Returns(settings);
            var service = new GeminiService(_httpClient, _mockOptions.Object);

            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            // Act
            Func<Task> act = () => service.GenerateHolidayVisualConfigAsync("Holiday");

            // Assert
            await act.Should().ThrowAsync<HttpRequestException>();
        }

        #endregion

        private class LocalFakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; }
                = req => new HttpResponseMessage(HttpStatusCode.OK);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Handler(request));
            }
        }
    }
}