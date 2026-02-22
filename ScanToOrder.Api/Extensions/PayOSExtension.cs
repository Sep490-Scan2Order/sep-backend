using Microsoft.Extensions.Options;
using PayOS;
using ScanToOrder.Infrastructure.Configuration;

namespace ScanToOrder.Api.Extensions;

public static class PayOSExtension
{
    public static IServiceCollection AddPayOSConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayOSSettings>(configuration.GetSection("PayOSSettings"));
        // PayOS setting
        services.AddScoped(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PayOSSettings>>().Value;
            return new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        });
        return services;
    }
}