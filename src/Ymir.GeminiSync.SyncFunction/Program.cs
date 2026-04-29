using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddDbContext<WasteManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WasteManagementContext")));
builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();

builder.Build().Run();
