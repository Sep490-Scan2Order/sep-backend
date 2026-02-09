using ScanToOrder.Application.Mappings;

namespace ScanToOrder.Api.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection AddDIConfig(this IServiceCollection services)
        {
            services.Scan(scan => scan
                .FromAssemblies(
                    typeof(ScanToOrder.Application.Mappings.GeneralProfile).Assembly,
                    typeof(ScanToOrder.Domain.Interfaces.IGenericRepository).Assembly,
                    typeof(ScanToOrder.Infrastructure.Context.DbContext).Assembly
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
