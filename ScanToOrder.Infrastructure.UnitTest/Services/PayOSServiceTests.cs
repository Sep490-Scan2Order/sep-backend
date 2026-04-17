using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using ScanToOrder.Application.DTOs.Payment;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class PayOSServiceTests
{
    private readonly Mock<IPayOSClientAdapter> _clientAdapter = new(MockBehavior.Strict);
    private readonly Mock<IPayOSPaymentRequestsAdapter> _paymentRequests = new(MockBehavior.Strict);
    private readonly Mock<IPayOSWebhooksAdapter> _webhooks = new(MockBehavior.Strict);
    private readonly PayOSService _sut;

    public PayOSServiceTests()
    {
        _clientAdapter.SetupGet(x => x.PaymentRequests).Returns(_paymentRequests.Object);
        _clientAdapter.SetupGet(x => x.Webhooks).Returns(_webhooks.Object);

        _sut = new PayOSService(_clientAdapter.Object, Options.Create(new PayOSSettings()));
    }

    [Fact]
    public async Task CreatePaymentLinkAsync_MapsRequestAndReturnsCheckoutUrl()
    {
        var request = new CreatePaymentRequest
        {
            OrderCode = 12345,
            Amount = 15000,
            Description = "Thanh toan don hang",
            CancelUrl = "https://cancel",
            ReturnUrl = "https://return"
        };

        _paymentRequests
            .Setup(x => x.CreateAsync(It.Is<CreatePaymentLinkRequest>(mapped =>
                mapped.OrderCode == request.OrderCode &&
                mapped.Amount == request.Amount &&
                mapped.Description == request.Description &&
                mapped.CancelUrl == request.CancelUrl &&
                mapped.ReturnUrl == request.ReturnUrl)))
            .ReturnsAsync(Mock.Of<IPayOSCreatePaymentLinkResponse>(x => x.CheckoutUrl == "https://pay.payos.vn/link"));

        var result = await _sut.CreatePaymentLinkAsync(request);

        result.Should().Be("https://pay.payos.vn/link");
        _paymentRequests.VerifyAll();
    }

    [Theory]
    [InlineData("PAID", "01", true)]
    [InlineData("SUCCESS", "01", true)]
    [InlineData("PENDING", "00", true)]
    [InlineData("PENDING", "01", false)]
    public async Task IsPaymentSuccessfulAsync_ReturnsExpectedValue_BasedOnStatusAndCode(string status, string code, bool expected)
    {
        _paymentRequests
            .Setup(x => x.GetAsync(123456))
            .ReturnsAsync(new { status, code });

        var result = await _sut.IsPaymentSuccessfulAsync(123456);

        result.Should().Be(expected);
        _paymentRequests.VerifyAll();
    }

    [Fact]
    public async Task IsPaymentSuccessfulAsync_WhenStatusIsNull_UsesEmptyStringAndStillChecksCode()
    {
        _paymentRequests
            .Setup(x => x.GetAsync(200001))
            .ReturnsAsync(new { status = (string?)null, code = "00" });

        var result = await _sut.IsPaymentSuccessfulAsync(200001);

        result.Should().BeTrue();
        _paymentRequests.VerifyAll();
    }

    [Fact]
    public async Task IsPaymentSuccessfulAsync_WhenCodeIsNull_UsesEmptyStringAndReturnsFalseWhenStatusNotPaid()
    {
        _paymentRequests
            .Setup(x => x.GetAsync(200002))
            .ReturnsAsync(new { status = "PENDING", code = (string?)null });

        var result = await _sut.IsPaymentSuccessfulAsync(200002);

        result.Should().BeFalse();
        _paymentRequests.VerifyAll();
    }

    [Fact]
    public async Task VerifyWebhookAsync_MapsVerifiedWebhookToPaymentResult()
    {
        var request = new Webhook
        {
            Success = true,
            Data = new WebhookData
            {
                CounterAccountBankId = "VCB",
                CounterAccountNumber = "123456789"
            }
        };

        var verified = new WebhookData
        {
            OrderCode = 12345,
            Amount = 50000,
            Description = "Thanh toan don hang",
            Code = "00",
            CounterAccountName = "NGUYEN VAN A",
            Reference = "REF123"
        };

        _webhooks.Setup(x => x.VerifyAsync(request)).ReturnsAsync(verified);

        var result = await _sut.VerifyWebhookAsync(request);

        result.OrderCode.Should().Be(12345);
        result.Amount.Should().Be(50000);
        result.Description.Should().Be("Thanh toan don hang");
        result.Reference.Should().Be("REF123");
        result.IsPaymentSuccess.Should().BeTrue();
        result.BankBin.Should().Be("VCB");
        result.AccountNumber.Should().Be("123456789");
        _webhooks.VerifyAll();
    }

    [Theory]
    [InlineData(false, "00", false)]
    [InlineData(true, "01", false)]
    public async Task VerifyWebhookAsync_MapsFalseWhenEitherSideIsNotSuccessful(bool requestSuccess, string code, bool expected)
    {
        var request = new Webhook
        {
            Success = requestSuccess,
            Data = new WebhookData
            {
                CounterAccountBankId = "VCB",
                CounterAccountNumber = "123456789"
            }
        };

        _webhooks.Setup(x => x.VerifyAsync(request)).ReturnsAsync(new WebhookData
        {
            OrderCode = 1,
            Amount = 10,
            Description = "Test",
            Code = code
        });

        var result = await _sut.VerifyWebhookAsync(request);

        result.IsPaymentSuccess.Should().Be(expected);
        _webhooks.VerifyAll();
    }

    [Fact]
    public async Task VerifyWebhookAsync_BubblesAdapterException()
    {
        var request = new Webhook { Success = true, Data = new WebhookData() };
        _webhooks.Setup(x => x.VerifyAsync(request)).ThrowsAsync(new Exception("integrity"));

        var action = () => _sut.VerifyWebhookAsync(request);

        await action.Should().ThrowAsync<Exception>().WithMessage("*integrity*");
    }

    [Fact]
    public void MapToPaymentResult_MapsFieldsAndSuccess()
    {
        var request = new Webhook
        {
            Success = true,
            Data = new WebhookData
            {
                CounterAccountBankId = "VCB",
                CounterAccountNumber = "123456789"
            }
        };

        var verified = new WebhookData
        {
            OrderCode = 123,
            Amount = 50000,
            Description = "Test mapping",
            Code = "00",
            CounterAccountName = "NGUYEN VAN A",
            Reference = "REF123"
        };

        var result = _sut.MapToPaymentResult(request, verified);

        result.OrderCode.Should().Be(123);
        result.Amount.Should().Be(50000);
        result.Description.Should().Be("Test mapping");
        result.Reference.Should().Be("REF123");
        result.IsPaymentSuccess.Should().BeTrue();
        result.BankBin.Should().Be("VCB");
        result.AccountNumber.Should().Be("123456789");
    }

    [Fact]
    public void MapToPaymentResult_ReturnsFalse_WhenRequestNotSuccessfulOrCodeNot00()
    {
        var request = new Webhook
        {
            Success = false,
            Data = new WebhookData
            {
                CounterAccountBankId = "VCB",
                CounterAccountNumber = "123456789"
            }
        };

        var verified = new WebhookData
        {
            OrderCode = 1,
            Amount = 100,
            Description = "Test",
            Code = "01"
        };

        var result = _sut.MapToPaymentResult(request, verified);

        result.IsPaymentSuccess.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"status\":\"PAID\"}", "status", "PAID")]
    [InlineData("{\"STATUS\":\"SUCCESS\"}", "status", "SUCCESS")]
    [InlineData("{\"status\":123}", "status", null)]
    [InlineData("{\"other\":\"value\"}", "status", null)]
    public void TryGetString_ReturnsExpectedValue(string json, string propertyName, string? expected)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var result = InvokeTryGetString(root, propertyName);

        result.Should().Be(expected);
    }

    private static string? InvokeTryGetString(JsonElement root, string propertyName)
    {
        var method = typeof(PayOSService).GetMethod("TryGetString", BindingFlags.NonPublic | BindingFlags.Static);
        return method!.Invoke(null, new object[] { root, propertyName }) as string;
    }
}