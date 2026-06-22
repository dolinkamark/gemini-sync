using Microsoft.EntityFrameworkCore;
using Serilog;
using System.CommandLine;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.Importer;
using Ymir.GeminiSync.Services;
using Ymir.GeminiSync.Services.Abstract;

var entityOption = new Option<string?>("--entity")
{
    Description = "The entity to import/sync/delete."
};

var deleteOption = new Option<bool>("--delete")
{
    Description = "Delete the selected entity data before processing."
};

var rootCommand = new RootCommand("GeminiSync importer")
{
    entityOption,
    deleteOption
};

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
builder.Services.AddTransient<IGarbageBinCollectionService, GarbageBinCollectionService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static RootCommand CreateRootCommand(string[] args)
{
    var entityOption = new Option<string?>("--entity")
    {
        Description = "The entity to import/sync/delete."
    };

    var deleteOption = new Option<bool>("--delete")
    {
        Description = "Delete the selected entity data before processing."
    };

    return new RootCommand("GeminiSync importer")
    {
        entityOption,
        deleteOption
    };
}
