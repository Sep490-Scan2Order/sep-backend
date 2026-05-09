using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.ML;
using ScanToOrder.Api.Extensions;
using ScanToOrder.Api.Filters;
using ScanToOrder.Api.Middleware;
using ScanToOrder.Infrastructure.Hubs;
using System.Text.Json;
using System.Threading.RateLimiting;
using ScanToOrder.Application.Wrapper;
    
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerConfig();
builder.Services.AddDIConfig(builder.Configuration);
builder.Services.AddAuthConfig(builder.Configuration);
builder.Services.AddExternalUtilsConfig(builder.Configuration);
builder.Services.AddRedisCloudServices(builder.Configuration);
builder.Services.AddEmailServices(builder.Configuration);
builder.Services.AddPayOSConfig(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddBackgroundJobs(builder.Configuration);

// [AI Upsell] Register PredictionEnginePool for ML.NET - auto-loads if model exists
var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartUpsellModel.zip");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress;
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Failure("Too many requests. Please try again later.", null);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        await context.HttpContext.Response.WriteAsJsonAsync(response, jsonOptions, cancellationToken: token);
    };

    options.AddPolicy("ip-limit", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0
        });
    });
});

builder.Services
    .AddPredictionEnginePool<ScanToOrder.Infrastructure.Models.AI.DishCoOccurrence,
        ScanToOrder.Infrastructure.Models.AI.DishPrediction>()
    .FromFile(modelName: "UpsellModel", filePath: modelPath, watchForChanges: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { },
    KnownProxies = { }
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.RegisterCronJobs();

app.UseMiddleware<HandleExceptionMiddleware>();
app.UseRateLimiter();
app.UseCors("AllowFrontend");
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<Scan2OrderRealtimeHub>("/scan2order-hub");

app.MapControllers();

app.Run();