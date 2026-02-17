namespace ScanToOrder.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Template;
using ScanToOrder.Application.Wrapper;
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

    public async Task SaveOtpAsync(string email, string otpCode, string purpose)
    {
        var key = GetKey(email, purpose);
        await _database.StringSetAsync(key, otpCode, TimeSpan.FromMinutes(30));
    }

    public async Task<ApiResponse<string?>> GetOtpAsync(string email, string purpose)
    {
        var key = GetKey(email, purpose);
        return new ApiResponse<string?>
        {
            IsSuccess = true,
            Data = await _database.StringGetAsync(key)
        };
    }

    public async Task DeleteOtpAsync(string email, string purpose)
    {
        var key = GetKey(email, purpose);
        await _database.KeyDeleteAsync(key);
    }

    public async Task<ApiResponse<string>> GenerateAndSaveOtpAsync(string email, string purpose)
    {
        Random generator = new Random();
        string otpCode = generator.Next(100000, 999999).ToString();

        await SaveOtpAsync(email, otpCode, purpose);

        string templateId = ResendTemplate.REGISTER_TEMPLATE_ID;
        var templateParams = new
        {
            OTP = int.Parse(otpCode)
        };

        await _emailService.SendEmailWithTemplateAsync(
            email,
            "Xác minh tài khoản Scan2Order",
            templateId,
            templateParams
        );

        return new ApiResponse<string>
        {
            IsSuccess = true,
            Message = OtpMessage.OtpSuccess.OTP_GENERATED,
            Data = otpCode
        };
    }
}