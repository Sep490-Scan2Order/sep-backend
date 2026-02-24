namespace ScanToOrder.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Template;
using StackExchange.Redis;

public class OtpRedisService : IOtpRedisService
{
    private readonly IDatabase _database;
    private readonly IEmailService _emailService;
    private readonly string _instanceName;

    public OtpRedisService(IConnectionMultiplexer redis, IConfiguration config, IEmailService emailService)
    {
        _database = redis.GetDatabase();
        _instanceName = config["RedisSettings:InstanceName"] ?? "";
        _emailService = emailService;
    }

    private string GetKey(string email, string purpose)
        => $"{_instanceName}otp:{purpose}:{email}";

    public async Task SaveOtpTenantAsync(string email, string otpCode, string purpose)
    {
        var key = GetKey(email, purpose);
        await _database.StringSetAsync(key, otpCode, TimeSpan.FromMinutes(30));
    }

    public async Task<string?> GetOtpTenantAsync(string email, string purpose)
    {
        var key = GetKey(email, purpose);
        return await _database.StringGetAsync(key);
    }

    public async Task DeleteOtpTenantAsync(string email, string purpose)
    {
        var key = GetKey(email, purpose);
        await _database.KeyDeleteAsync(key);
    }

    public async Task<string> GenerateAndSaveOtpTenantAsync(string email, string purpose)
    {
        Random generator = new Random();
        string otpCode = generator.Next(100000, 999999).ToString();

        await SaveOtpTenantAsync(email, otpCode, purpose);

        string templateId;
        string subject;

        switch (purpose)
        {
            case OtpMessage.OtpKeyword.OTP_REGISTER:
                templateId = ResendTemplate.REGISTER_TENANT_TEMPLATE_ID;
                subject = EmailMessage.EmailSubject.REGISTER_SUBJECT;
                break;

            case OtpMessage.OtpKeyword.OTP_FORGOT_PASSWORD:
                templateId = ResendTemplate.FORGOT_PASSWORD_TENANT_TEMPLATE_ID;
                subject = EmailMessage.EmailSubject.FORGOT_PASSWORD_SUBJECT;
                break;

            case OtpMessage.OtpKeyword.OTP_RESET_PASSWORD:
                templateId = ResendTemplate.RESET_PASSWORD_TENANT_TEMPLATE_ID;
                subject = EmailMessage.EmailSubject.RESET_PASSWORD_SUBJECT;
                break;

            default:
                templateId = ResendTemplate.REGISTER_TENANT_TEMPLATE_ID;
                subject = EmailMessage.EmailSubject.DEFAULT_SUBJECT;
                break;
        }

        var templateParams = new
        {
            OTP = otpCode,
            ExpiryTime = DateTime.UtcNow.AddMinutes(5).ToString("HH:mm:ss")
        };

        await _emailService.SendEmailWithTemplateIdDomainAsync(
            email,
            subject,
            templateId,
            templateParams
        );

        return otpCode;
    }
}