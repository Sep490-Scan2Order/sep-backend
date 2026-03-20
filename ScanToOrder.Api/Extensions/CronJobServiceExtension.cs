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

        TimeZoneInfo vnTimeZone;
        try
        {
            vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }

        var options = new RecurringJobOptions { TimeZone = vnTimeZone };

        recurringJobManager.AddOrUpdate<ICronJobService>(
            "Cancel-Expired-Unpaid-Orders", 
            job => job.CancelExpiredUnpaidOrdersAsync(CancellationToken.None), 
            Cron.MinuteInterval(5),
            options
        );

        recurringJobManager.AddOrUpdate<ICronJobService>(
            "Sync-Branch-Dish-Selling-Status", 
            job => job.SyncBranchDishSellingStatusAsync(CancellationToken.None), 
            Cron.Daily(2, 00),
            options
        );

        recurringJobManager.AddOrUpdate<ICronJobService>(
            "Sync-Branch-Dish-Price",
            job => job.SyncBranchDishPriceAsync(CancellationToken.None),
            Cron.Daily(2, 00),
            options
        );
    }
}