using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.Importer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
