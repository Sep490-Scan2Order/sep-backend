using StackExchange.Redis;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Api.Extensions
{
    public static class RedisExtension
    {
        public static IServiceCollection AddRedisCloudServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration["RedisSettings:ConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Redis ConnectionString is missing in appsettings.json");
            }

            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(connectionString));

            return services;
        }
    }
}