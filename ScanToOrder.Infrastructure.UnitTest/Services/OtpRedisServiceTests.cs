using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Template;
using ScanToOrder.Infrastructure.Services;
using StackExchange.Redis;

namespace ScanToOrder.Infrastructure.UnitTest.Services;

public class OtpRedisServiceTests
{
    private static bool HasTemplateOtpAndExpiry(object templateParams, string expectedOtp)
    {
        var otpProperty = templateParams.GetType().GetProperty("OTP");
        var expiryProperty = templateParams.GetType().GetProperty("ExpiryTime");

        return otpProperty != null
               && expiryProperty != null
               && otpProperty.GetValue(templateParams)?.ToString() == expectedOtp;
    }

    private static void AssertStringSetInvocation(Mock<IDatabase> database, string expectedKey, string expectedValue, string expectedExpiration)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.StringSetAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
        invocation.Arguments[1].ToString().Should().Be(expectedValue);
        invocation.Arguments[2].ToString().Should().Be(expectedExpiration);
    }

    private static void AssertStringGetInvocation(Mock<IDatabase> database, string expectedKey)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.StringGetAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
    }

    private static void AssertKeyDeleteInvocation(Mock<IDatabase> database, string expectedKey)
    {
        var invocation = database.Invocations.Single(invocation => invocation.Method.Name == nameof(IDatabase.KeyDeleteAsync));

        invocation.Arguments[0].ToString().Should().Be(expectedKey);
    }

    private static (OtpRedisService sut, Mock<IDatabase> database, Mock<IEmailService> emailService, Mock<IConfiguration> config)
        CreateSut(string? instanceName = null)
    {
        var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var database = new Mock<IDatabase>(MockBehavior.Loose);
        var emailService = new Mock<IEmailService>(MockBehavior.Loose);
        var config = new Mock<IConfiguration>(MockBehavior.Strict);

        connection
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        config.SetupGet(x => x["RedisSettings:InstanceName"]).Returns(instanceName);
        emailService
            .Setup(x => x.SendEmailWithTemplateIdDomainAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()))
            .ReturnsAsync(true);

        var sut = new OtpRedisService(connection.Object, config.Object, emailService.Object);
        return (sut, database, emailService, config);
    }

    [Fact]
    public async Task SaveOtpTenantAsync_WhenInstanceNameIsNull_UsesDefaultKeyAndThirtyMinuteTtl()
    {
        var (sut, database, _, _) = CreateSut(null);
        var email = "test@gmail.com";
        var otp = "123456";
        var purpose = "test";
        var expectedKey = "otp:test:test@gmail.com";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOtpTenantAsync(email, otp, purpose);

        AssertStringSetInvocation(database, expectedKey, otp, "EX 1800");
    }

    [Fact]
    public async Task SaveOtpTenantAsync_WhenInstanceNameIsSet_PrefixesRedisKey()
    {
        var (sut, database, _, _) = CreateSut("S2O:");
        var email = "tenant@test.com";
        var otp = "654321";
        var purpose = "login";
        var expectedKey = "S2O:otp:login:tenant@test.com";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOtpTenantAsync(email, otp, purpose);

        AssertStringSetInvocation(database, expectedKey, otp, "EX 1800");
    }

    [Fact]
    public async Task GetOtpTenantAsync_ReturnsValueUsingExactRedisKey()
    {
        var (sut, database, _, _) = CreateSut("S2O:");
        var email = "test@gmail.com";
        var purpose = "forgot";
        var expectedKey = "S2O:otp:forgot:test@gmail.com";
        const string expectedOtp = "111222";

        database
            .Setup(db => db.StringGetAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(expectedOtp);

        var result = await sut.GetOtpTenantAsync(email, purpose);

        result.Should().Be(expectedOtp);
        AssertStringGetInvocation(database, expectedKey);
    }

    [Fact]
    public async Task SaveOtpCustomerAsync_UsesProvidedExpiryAndCustomerKey()
    {
        var (sut, database, _, _) = CreateSut();
        var phone = "0901234567";
        var otp = "222333";
        var purpose = "login";
        var expiry = TimeSpan.FromMinutes(15);
        var expectedKey = "otp:login:0901234567";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.SaveOtpCustomerAsync(phone, otp, purpose, expiry);

        AssertStringSetInvocation(database, expectedKey, otp, "EX 900");
    }

    [Fact]
    public async Task DeleteOtpTenantAsync_DeletesExactRedisKey()
    {
        var (sut, database, _, _) = CreateSut("S2O:");
        var email = "test@gmail.com";
        var purpose = "reset";
        var expectedKey = "S2O:otp:reset:test@gmail.com";

        database
            .Setup(db => db.KeyDeleteAsync(expectedKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await sut.DeleteOtpTenantAsync(email, purpose);

        AssertKeyDeleteInvocation(database, expectedKey);
    }

    [Theory]
    [InlineData(OtpMessage.OtpKeyword.OTP_REGISTER, ResendTemplate.REGISTER_TENANT_TEMPLATE_ID, EmailMessage.EmailSubject.REGISTER_SUBJECT)]
    [InlineData(OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD, ResendTemplate.FORGOT_PASSWORD_TENANT_TEMPLATE_ID, EmailMessage.EmailSubject.FORGOT_PASSWORD_SUBJECT)]
    [InlineData(OtpMessage.OtpKeyword.OTP_RESET_PASSWORD, ResendTemplate.RESET_PASSWORD_TENANT_TEMPLATE_ID, EmailMessage.EmailSubject.RESET_PASSWORD_SUBJECT)]
    public async Task GenerateAndSaveOtpTenantAsync_UsesTemplateMatchingPurpose(string purpose, string expectedTemplateId, string expectedSubject)
    {
        var (sut, database, emailService, _) = CreateSut("S2O:");
        var email = "tenant@test.com";
        var expectedKey = $"S2O:otp:{purpose}:{email}";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var otpResult = await sut.GenerateAndSaveOtpTenantAsync(email, purpose);

        otpResult.Should().HaveLength(6);
        AssertStringSetInvocation(database, expectedKey, otpResult, "EX 1800");
        emailService.Verify(x => x.SendEmailWithTemplateIdDomainAsync(
            email,
            expectedSubject,
            expectedTemplateId,
            It.Is<object>(obj => HasTemplateOtpAndExpiry(obj, otpResult))), Times.Once);
    }

    [Fact]
    public async Task GenerateAndSaveOtpTenantAsync_WhenPurposeIsUnknown_UsesDefaultTemplate()
    {
        var (sut, database, emailService, _) = CreateSut();
        var email = "unknown@test.com";
        const string purpose = "unknown-purpose";
        var expectedKey = "otp:unknown-purpose:unknown@test.com";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var otpResult = await sut.GenerateAndSaveOtpTenantAsync(email, purpose);

        otpResult.Should().HaveLength(6);
        AssertStringSetInvocation(database, expectedKey, otpResult, "EX 1800");
        emailService.Verify(x => x.SendEmailWithTemplateIdDomainAsync(
            email,
            EmailMessage.EmailSubject.DEFAULT_SUBJECT,
            ResendTemplate.REGISTER_TENANT_TEMPLATE_ID,
            It.Is<object>(obj => HasTemplateOtpAndExpiry(obj, otpResult))), Times.Once);
    }

    [Fact]
    public async Task GenerateAndSaveStaffForgotOtpAsync_UsesStaffPurposeKeyAndStaffTemplate()
    {
        var (sut, database, emailService, _) = CreateSut("S2O:");
        var email = "staff@store.com";
        var expectedKey = $"S2O:otp:{OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD_STAFF}:{email}";

        database
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var otpResult = await sut.GenerateAndSaveStaffForgotOtpAsync(email);

        otpResult.Should().HaveLength(6);
        AssertStringSetInvocation(database, expectedKey, otpResult, "EX 1800");
        emailService.Verify(x => x.SendEmailWithTemplateIdDomainAsync(
            email,
            EmailMessage.EmailSubject.FORGOT_PASSWORD_SUBJECT,
            ResendTemplate.FORGOT_PASSWORD_STAFF_TEMPLATE_ID,
            It.Is<object>(obj => HasTemplateOtpAndExpiry(obj, otpResult))), Times.Once);
    }
}