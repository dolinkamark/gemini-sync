using Microsoft.EntityFrameworkCore;
using Serilog;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.Importer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddDbContext<WasteManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WasteManagement")));
builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
