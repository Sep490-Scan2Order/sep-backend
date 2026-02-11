using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Mappings;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Api.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection AddDIConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            });

            services.Scan(scan => scan
                .FromAssemblies(
                    typeof(ScanToOrder.Application.Mappings.GeneralProfile).Assembly,
                    typeof(ScanToOrder.Domain.Interfaces.IGenericRepository<>).Assembly,
                    typeof(ScanToOrder.Infrastructure.Context.AppDbContext).Assembly
                )
                .AddClasses(classes => classes.Where(type =>
                    !typeof(IHostedService).IsAssignableFrom(type) &&
                    !typeof(Exception).IsAssignableFrom(type)))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            );

            services.AddHttpContextAccessor();
            services.AddAutoMapper(typeof(GeneralProfile).Assembly);
            return services;
        }
    }
}
