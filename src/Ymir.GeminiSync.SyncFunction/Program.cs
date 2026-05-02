using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.Services.Settings;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddDbContext<WasteManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WasteManagementContext")));

var geminiSettings = builder.Configuration
    .GetSection(nameof(GeminiSettings))
    .Get<GeminiSettings>() ?? new GeminiSettings();
builder.Services.AddSingleton(geminiSettings);

builder.Services.AddHttpClient();
builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();

builder.Build().Run();
