using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using FluentAssertions;
using ScanToOrder.Infrastructure.Services;
using ScanToOrder.Application.DTOs.External;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class BankLookupServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<ILogger<BankLookupService>> _mockLogger;
    private readonly HttpClient _httpClient;
    private readonly BankLookupService _service;

    public BankLookupServiceTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockLogger = new Mock<ILogger<BankLookupService>>();
        
        // Tạo HttpClient với Mock Handler
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://api.test.com/")
        };

        _service = new BankLookupService(_httpClient, _mockLogger.Object);
    }

    [Fact]
    public async Task LookupAccountAsync_WhenApiReturnsSuccess_ShouldReturnBankLookResponse()
    {
        // Arrange
        var request = new BankLookRequest { Bank = "970415", Account = "123456" };
        var expectedResponse = new BankLookResponse 
        { 
            Success = true, 
            Data = new BankLookData { OwnerName = "NGUYEN VAN A" } 
        };

        SetupMockHttpResponse(HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _service.LookupAccountAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.OwnerName.Should().Be("NGUYEN VAN A");
    }

    [Fact]
    public async Task LookupAccountAsync_WhenApiReturns200OK_ShouldReturnSuccess()
    {
        var request = new BankLookRequest 
        { 
            Bank = "970415", 
            Account = "123456789" 
        };

        var mockResponseData = new BankLookResponse
        {
            Success = true,
            Msg = "Truy vấn thành công",
            Data = new BankLookData 
            { 
                OwnerName = "NGUYEN VAN A" 
            }
        };

        // Giả lập HttpClient trả về mã 200 OK kèm theo JSON content
        var responseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(mockResponseData) // Sử dụng System.Net.Http.Json để tạo content
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // 2. Act: Gọi service
        var result = await _service.LookupAccountAsync(request);

        // 3. Assert: Kiểm tra kết quả trả về phải là thành công
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.OwnerName.Should().Be("NGUYEN VAN A");
        result.Msg.Should().Be("Truy vấn thành công");

        // Kiểm tra xem LogInformation có được gọi đúng như trong code không
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Truy vấn thành công")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LookupAccountAsync_WhenApiReturnsErrorStatusCode_ShouldLogErrorAndReturnFailure()
    {
        // Arrange
        var request = new BankLookRequest { Bank = "970415", Account = "123" };
        var errorContent = "Internal Server Error";

        // Giả lập trả về mã lỗi 500
        SetupMockHttpResponse(HttpStatusCode.InternalServerError, errorContent);

        // Act
        var result = await _service.LookupAccountAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Msg.Should().Contain("500");

        // Kiểm tra LogError có được gọi không
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("API BankLookup lỗi hệ thống")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LookupAccountAsync_WhenApiResponseIsNull_ShouldLogWarningAndReturnNull()
    {
        // Arrange
        var request = new BankLookRequest { Bank = "970415", Account = "123" };

        // Giả lập API trả về nội dung rỗng (null khi deserialize)
        var response = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _service.LookupAccountAsync(request);

        // Assert
        result.Should().BeNull();

        // Kiểm tra LogWarning có được gọi khi result null không
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("API trả về thất bại")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LookupAccountAsync_WhenApiReturnsSuccessFalse_ShouldLogWarningAndReturnResult()
    {
        // Arrange
        var request = new BankLookRequest { Bank = "970415", Account = "111" };
        var expectedResponse = new BankLookResponse 
        { 
            Success = false, 
            Msg = "Số tài khoản không tồn tại" 
        };

        SetupMockHttpResponse(HttpStatusCode.OK, expectedResponse);

        // Act
        var result = await _service.LookupAccountAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Msg.Should().Be("Số tài khoản không tồn tại");
    }

    [Fact]
    public async Task LookupAccountAsync_WhenExceptionOccurs_ShouldReturnInternalErrorMessage()
    {
        // Arrange
        var request = new BankLookRequest();
        
        // Giả lập HttpClient ném ra ngoại lệ (ví dụ: DNS error hoặc Timeout)
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new Exception("Network failure"));

        // Act
        var result = await _service.LookupAccountAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Msg.Should().Be("Đã xảy ra lỗi trong quá trình xử lý yêu cầu.");
    }

    #region Helpers
    
    private void SetupMockHttpResponse<T>(HttpStatusCode statusCode, T content)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = JsonContent.Create(content)
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupMockHttpResponse(HttpStatusCode statusCode, string content)
    {
        var response = new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(content)
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
    
    #endregion
}