using Microsoft.Extensions.Options;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Api.Extensions
{
    public static class ExternalUtilsExtension
    {
        public static IServiceCollection AddExternalUtilsConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<N8NSettings>(configuration.GetSection("N8NSettings"));

            services.AddHttpClient<ITaxService, TaxService>(client =>
            {
                var apiKey = configuration["N8NSettings:ApiKey"];
                client.DefaultRequestHeaders.Add("s2o-api-key", apiKey);
            });
            
            services.Configure<BankLookupSettings>(configuration.GetSection("BankLookupSettings"));
            services.AddHttpClient<IBankLookupService, BankLookupService>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<BankLookupSettings>>().Value;

                client.BaseAddress = new Uri(settings.BaseUrl);
                client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
                client.DefaultRequestHeaders.Add("x-api-secret", settings.ApiSecret);
            });
            return services;
        }
    }
}
