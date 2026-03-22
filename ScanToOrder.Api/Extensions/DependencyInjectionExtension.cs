using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenAI;
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
            // services.AddDbContext<AppDbContext>(options =>
            // {
            //     options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
            //         o =>
            //         {
            //             o.UseNetTopologySuite();
            //             o.UseVector();
            //             o.CommandTimeout(120);
            //         });
            // });
            
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            
            dataSourceBuilder.UseNetTopologySuite();
            dataSourceBuilder.EnableDynamicJson();
            dataSourceBuilder.UseVector();
            
            var dataSource = dataSourceBuilder.Build();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(dataSource, o =>
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
                    !typeof(Exception).IsAssignableFrom(type) &&
                    type.Name != "EfDbTransaction"))
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            );
            
            services.Configure<EsmsSettings>(configuration.GetSection("EsmsSettings"));
            services.Configure<VpsSettings>(configuration.GetSection("VpsSettings"));
            services.Configure<OpenAiSettings>(configuration.GetSection("OpenAiSettings"));
            services.Configure<SupabaseSettings>(configuration.GetSection("Supabase"));
            services.AddHttpClient<ISmsSender, EsmsSender>();
            services.AddHttpClient<IGeminiService, GeminiService>();
            services.AddHttpClient<IHuggingFaceService, HuggingFaceService>();
            
            // OpenAI setting
            services.AddScoped<OpenAIClient>(op =>
            {
                var settings = op.GetRequiredService<IOptions<OpenAiSettings>>().Value;
                return new OpenAIClient(settings.ApiKey);
            });
            
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddAutoMapper(typeof(GeneralProfile).Assembly);
            
            return services;
        }
    }
}
