using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.CommandLine;
using Ymir.GeminiSync.Domain.Repositories;
using Ymir.GeminiSync.EntityFramework;
using Ymir.GeminiSync.EntityFramework.Repositories;
using Ymir.GeminiSync.Importer;
using Ymir.GeminiSync.Importer.Models;
using Ymir.GeminiSync.Services;
using Ymir.GeminiSync.Services.Abstract;

var entityOption = new Option<string?>("--entities")
{
    Description = "The entities to import/sync/delete."
};

var deleteOption = new Option<bool>("--delete")
{
    Description = "Delete the selected entity data before processing."
};

var customerIdOption = new Option<int?>("--customer-id")
{
    Description = "Customer id to process."
};

var placeTypeDescriptionOption = new Option<string>("--place-type-description")
{
    Description = "Place type description to filter on."
};

var useFileCache = new Option<bool>("--use-file-cache")
{
    Description = "Flag whether to cache results for testing."
};

var rootCommand = CreateRootCommand();
var parseResult = rootCommand.Parse(args);

if (parseResult.Errors.Count > 0)
{
    return parseResult.Invoke();
}

var builder = Host.CreateApplicationBuilder(args);
var importerOptions = BuildImporterOptions(builder.Configuration, parseResult);

builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddDbContext<WasteManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WasteManagement")));

builder.Services.AddSingleton(Options.Create(importerOptions));

builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();
builder.Services.AddTransient<IGarbageBinCollectionRepository, GarbageBinCollectionRepository>();
builder.Services.AddTransient<IAgreementPlacesRepository, AgreementPlacesRepository>();

builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
await host.RunAsync();

return Environment.ExitCode;

RootCommand CreateRootCommand()
{
    return new RootCommand("GeminiSync importer")
    {
        entityOption,
        deleteOption,
        customerIdOption,
        placeTypeDescriptionOption,
        useFileCache
    };
}

SyncOptions BuildImporterOptions(IConfiguration configuration, ParseResult parseResult)
{
    var configuredOptions = configuration
        .GetSection(SyncOptions.SectionName)
        .Get<SyncOptions>() ?? new SyncOptions();

    return new SyncOptions
    {
        Entities = parseResult.GetValue(entityOption) ?? configuredOptions.Entities,
        Delete = parseResult.GetValue(deleteOption) || configuredOptions.Delete,
        CustomerId = parseResult.GetValue(customerIdOption) ?? configuredOptions.CustomerId,
        PlaceTypeDescription = parseResult.GetValue(placeTypeDescriptionOption) ?? configuredOptions.PlaceTypeDescription,
        UseFileCache = parseResult.GetValue(useFileCache) || configuredOptions.UseFileCache,
    };
}
