using Microsoft.EntityFrameworkCore;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.ImportWorker;
using Ymir.GeminiSync.Services;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.Services.Settings;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<WasteManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WasteManagement")));

var geminiSettings = builder.Configuration
    .GetSection(nameof(GeminiSettings))
    .Get<GeminiSettings>() ?? new GeminiSettings();
builder.Services.AddSingleton(geminiSettings);

builder.Services.AddHttpClient();
builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();
builder.Services.AddTransient<IGeminiClient, GeminiClient>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
