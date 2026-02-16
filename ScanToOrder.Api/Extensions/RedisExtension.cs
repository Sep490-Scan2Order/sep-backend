using StackExchange.Redis;
using ScanToOrder.Application.Interfaces; 
using ScanToOrder.Infrastructure.Services; 

namespace ScanToOrder.Api.Extensions
{
    public static class RedisExtension
    {
        public static IServiceCollection AddRedisCloudServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Đọc ConnectionString từ appsettings.json
            var connectionString = configuration["RedisSettings:ConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Redis ConnectionString is missing in appsettings.json");
            }

            // 2. Đăng ký ConnectionMultiplexer là Singleton (Chỉ khởi tạo 1 lần duy nhất)
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(connectionString));

            // 3. Đăng ký Service xử lý OTP (Scoped hoặc Singleton tùy nhu cầu)
            // Ở đây tôi đăng ký IOtpRedisService mà chúng ta đã viết ở bước trước
            services.AddScoped<IOtpRedisService, OtpRedisService>();

            return services;
        }
    }
}