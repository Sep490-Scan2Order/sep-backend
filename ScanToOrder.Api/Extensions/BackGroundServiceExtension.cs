using Hangfire;
using Hangfire.PostgreSql;

namespace ScanToOrder.Api.Extensions;

public static class BackGroundServiceExtension
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(configuration.GetConnectionString("HangFireConnection"));
            }));

        services.AddHangfireServer();
        return services;
    } 
}