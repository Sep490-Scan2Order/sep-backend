using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Api.Extensions
{
    public static class EmailServiceExtension
    {
        public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<EmailSettings>("IO_DOMAIN", configuration.GetSection("scan2order.io.vn"));
            services.Configure<EmailSettings>("ID_DOMAIN", configuration.GetSection("scan2order.id.vn"));
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}