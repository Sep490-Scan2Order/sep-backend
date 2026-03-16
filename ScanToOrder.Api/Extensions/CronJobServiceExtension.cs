using Hangfire;
using Hangfire.PostgreSql;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Api.Extensions;

public static class CronJobServiceExtension
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
    
    public static void RegisterCronJobs(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        // recurringJobManager.AddOrUpdate<ICronJobService>(
        //     "Daily-TurnOff-Promotions", 
        //     job => job.DailyTurnOffPromotionsAsync(CancellationToken.None), 
        //     Cron.MinuteInterval(2)
        // );
    }
}