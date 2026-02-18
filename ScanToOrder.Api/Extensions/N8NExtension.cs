using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Services;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Api.Extensions
{
    public static class ExternalServiceExtension
    {
        public static IServiceCollection AddN8NServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<N8NSettings>(configuration.GetSection("N8NSettings"));

            services.AddHttpClient<ITaxService, TaxService>(client =>
            {
                var apiKey = configuration["N8NSettings:ApiKey"];
                client.DefaultRequestHeaders.Add("s2o-api-key", apiKey);
            });

            services.AddScoped<ITenantService, TenantService>();

            return services;
        }
    }
}
