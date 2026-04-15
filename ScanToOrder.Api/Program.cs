using Hangfire;
using Microsoft.Extensions.ML;
using Microsoft.AspNetCore.HttpOverrides;
using ScanToOrder.Api.Extensions;
using ScanToOrder.Api.Middleware;
using ScanToOrder.Infrastructure.Hubs;
using ScanToOrder.Api.Filters;

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
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.RegisterCronJobs();

app.UseMiddleware<HandleExceptionMiddleware>();
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