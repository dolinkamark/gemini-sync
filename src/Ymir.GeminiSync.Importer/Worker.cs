using Ymir.GeminiSync.Domain.Repositories;

namespace Ymir.GeminiSync.Importer;

public class Worker(
    ILogger<Worker> logger, 
    IGarbageBinCollectionRepository garbageBinRepository) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            const int customerId = 1;
            var garbageBins = await garbageBinRepository.GetGarbageBinCollections(customerId);



            await Task.Delay(1000, stoppingToken);
        }
    }
}
