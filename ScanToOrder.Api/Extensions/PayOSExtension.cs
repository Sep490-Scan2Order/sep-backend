using Microsoft.Extensions.Options;
using PayOS;
using ScanToOrder.Infrastructure.Services;
using ScanToOrder.Infrastructure.Configuration;

namespace ScanToOrder.Api.Extensions;

public static class PayOSExtension
{
    public static IServiceCollection AddPayOSConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayOSSettings>(configuration.GetSection("PayOSSettings"));
        services.AddScoped(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PayOSSettings>>().Value;
            return new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        });
        services.AddScoped<IPayOSClientAdapter, PayOSClientAdapter>();
        return services;
    }
}