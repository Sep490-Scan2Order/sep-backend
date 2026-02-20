using Microsoft.EntityFrameworkCore;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Mappings;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Context;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Api.Extensions
{
    public static class DependencyInjectionExtension
    {
        public static IServiceCollection AddDIConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                    o =>
                    {
                        o.UseNetTopologySuite();
                        o.UseVector();
                        o.CommandTimeout(120);
                    });
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
            services.Configure<EsmsSettings>(configuration.GetSection("EsmsSettings"));
            services.AddHttpClient<ISmsSender, EsmsSender>();
            
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddAutoMapper(typeof(GeneralProfile).Assembly);
            return services;
        }
    }
}
