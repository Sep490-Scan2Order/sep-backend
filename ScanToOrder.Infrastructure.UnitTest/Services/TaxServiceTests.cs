using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class TaxServiceTests
{
    private const string FakeUrl = "http://fake.url/tax";

    private static TaxService CreateSut(HttpClient httpClient)
    {
        var options = Options.Create(new N8NSettings { TaxValidationUrl = FakeUrl });
        var logger = new Mock<ILogger<TaxService>>();
        return new TaxService(httpClient, options, logger.Object);
    }

    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public List<HttpRequestMessage> Requests { get; } = new();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _handler(request, cancellationToken);
        }
    }

    private static HttpClient CreateHttpClient(HttpStatusCode status, string jsonBody, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        });

        return new HttpClient(handler);
    }

    private static HttpClient CreateThrowingHttpClient(Exception ex, out FakeHttpMessageHandler handler)
    {
        handler = new FakeHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(ex));
        return new HttpClient(handler);
    }

    #region IsTaxCodeValidAsync
    [Fact]
    public async Task IsTaxCodeValid_NullOrWhitespace_ReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{}", out var handler);
        var sut = CreateSut(httpClient);

        var result1 = await sut.IsTaxCodeValidAsync(null);
        var result2 = await sut.IsTaxCodeValidAsync("");
        var result3 = await sut.IsTaxCodeValidAsync("   ");

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task IsTaxCodeValid_HttpNonSuccess_ThrowsException()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "{\"err\":\"x\"}", out var handler);
        var sut = CreateSut(httpClient);

        var act = () => sut.IsTaxCodeValidAsync("0312345678");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Hệ thống kiểm tra mã số thuế không phản hồi.");
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task IsTaxCodeValid_ResponseActive_ReturnsTrue()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{\"taxStatus\":\"Đang hoạt động\"}", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.IsTaxCodeValidAsync("0312345678");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTaxCodeValid_ResponseInactive_ReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{\"taxStatus\":\"Đã chấm dứt\"}", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.IsTaxCodeValidAsync("0312345678");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTaxCodeValid_NullResult_ReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "null", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.IsTaxCodeValidAsync("0312345678");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTaxCodeValid_HttpClientThrows_Rethrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("boom"), out _);
        var sut = CreateSut(httpClient);

        var act = () => sut.IsTaxCodeValidAsync("0312345678");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("boom");
    }

    [Fact]
    public async Task IsTaxCodeValid_TaxStatusWithWhitespace_StillActive()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{\"taxStatus\":\" đang hoạt động \"}", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.IsTaxCodeValidAsync("0312345678");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsTaxCodeValid_TaxStatusNull_ReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{}", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.IsTaxCodeValidAsync("0312345678");

        result.Should().BeFalse();
    }
    #endregion

    #region GetTaxCodeDetailsAsync
    [Fact]
    public async Task GetTaxCodeDetails_NullOrWhitespace_ReturnsNull()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{}", out var handler);
        var sut = CreateSut(httpClient);

        var result1 = await sut.GetTaxCodeDetailsAsync(null);
        var result2 = await sut.GetTaxCodeDetailsAsync("");
        var result3 = await sut.GetTaxCodeDetailsAsync("   ");

        result1.Should().BeNull();
        result2.Should().BeNull();
        result3.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTaxCodeDetails_HttpNonSuccess_ThrowsException()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.NotFound, "{\"err\":\"x\"}", out _);
        var sut = CreateSut(httpClient);

        var act = () => sut.GetTaxCodeDetailsAsync("0312345678");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Hệ thống kiểm tra mã số thuế không phản hồi.");
    }

    [Fact]
    public async Task GetTaxCodeDetails_JsonArray_FirstItem_Active()
    {
        var body = "[{\"taxCode\":\"031\",\"fullName\":\"Cty A\",\"taxStatus\":\"Đang hoạt động\"}]";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.TaxCode.Should().Be("031");
        result.FullName.Should().Be("Cty A");
        result.Status.Should().Be("Đang hoạt động");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxCodeDetails_JsonArray_EmptyArray_ReturnsNull()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "[]", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_JsonObject_Active()
    {
        var body = "{\"taxCode\":\"031\",\"fullName\":\"Cty A\",\"taxStatus\":\"Đang hoạt động\"}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxCodeDetails_JsonObject_Inactive()
    {
        var body = "{\"taxCode\":\"031\",\"fullName\":\"Cty A\",\"taxStatus\":\"Ngừng hoạt động\"}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetTaxCodeDetails_TaxStatusNull_IsValidFalse()
    {
        var body = "{\"taxCode\":\"031\",\"fullName\":\"Cty A\",\"taxStatus\":null}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Status.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_ItemNull_ReturnsNull()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "null", out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawNull_SkipsRawParsing()
    {
        var body = "{\"taxCode\":\"031\",\"fullName\":\"Cty A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":null}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.TaxCode.Should().Be("031");
        result.IsPersonal.Should().BeFalse(); // default bool
        result.Representative.Should().BeNull();
        result.ManagedBy.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawHasPersonalTaxCode_SetsIsPersonalTrue()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Mã số thuế cá nhân\":\"0123456789\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsPersonal.Should().BeTrue();
        result.TaxCode.Should().Be("0123456789");
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawPersonalKeyNullValue_CoversNullToString()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Mã số thuế cá nhân\":null}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsPersonal.Should().BeTrue();
        result.TaxCode.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawHasBusinessTaxCode_SetsIsPersonalFalse()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Mã số thuế doanh nghiệp\":\"9876543210\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsPersonal.Should().BeFalse();
        result.TaxCode.Should().Be("9876543210");
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawBusinessKeyNullValue_CoversNullToString()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Mã số thuế doanh nghiệp\":null}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.IsPersonal.Should().BeFalse();
        result.TaxCode.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawHasRepresentative_SetsRepresentative()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Người đại diện\":\"Nguyen Van A\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.Representative.Should().Be("Nguyen Van A");
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawRepresentativeKeyNullValue_CoversNullToString()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Người đại diện\":null}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.Representative.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawHasManagedBy_SetsManagedBy()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Quản lý bởi\":\"Cục Thuế HCM\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.ManagedBy.Should().Be("Cục Thuế HCM");
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawManagedByKeyNullValue_CoversNullToString()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Quản lý bởi\":null}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.ManagedBy.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawHasAllFields_AllPropertiesSet()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Mã số thuế doanh nghiệp\":\"9876543210\",\"Người đại diện\":\"Nguyen Van A\",\"Quản lý bởi\":\"Cục Thuế HCM\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.TaxCode.Should().Be("9876543210");
        result.IsPersonal.Should().BeFalse();
        result.Representative.Should().Be("Nguyen Van A");
        result.ManagedBy.Should().Be("Cục Thuế HCM");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxCodeDetails_RawNoMatchingKeys_DefaultValues()
    {
        var body =
            "{\"taxCode\":\"031\",\"fullName\":\"A\",\"taxStatus\":\"Đang hoạt động\",\"raw\":{\"Khác\":\"zzz\"}}";
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body, out _);
        var sut = CreateSut(httpClient);

        var result = await sut.GetTaxCodeDetailsAsync("031");

        result.Should().NotBeNull();
        result.TaxCode.Should().Be("031");
        result.Representative.Should().BeNull();
        result.ManagedBy.Should().BeNull();
    }

    [Fact]
    public async Task GetTaxCodeDetails_HttpClientThrows_Rethrows()
    {
        var httpClient = CreateThrowingHttpClient(new HttpRequestException("boom"), out _);
        var sut = CreateSut(httpClient);

        var act = () => sut.GetTaxCodeDetailsAsync("031");

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("boom");
    }

    [Fact]
    public async Task GetTaxCodeDetails_InvalidJson_Rethrows()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{not-json", out _);
        var sut = CreateSut(httpClient);

        var act = () => sut.GetTaxCodeDetailsAsync("031");

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }
    #endregion
}
