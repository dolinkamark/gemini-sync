using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Ymir.GeminiSync.Services.Abstract;
using Ymir.GeminiSync.SyncFunction.Functions.Models;

namespace Ymir.GeminiSync.SyncFunction.Functions;

public sealed class SyncFunction(
    IGeminiClient geminiClient,
    ILogger<SyncFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(SyncFunction))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "v1/sync")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        SyncRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<SyncRequest>(
                request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON body on /api/v1/execute");
            return new BadRequestObjectResult(new { error = "Invalid JSON body." });
        }

        if (payload?.SyncedEntities is null)
        {
            return new BadRequestObjectResult(new { error = "Missing 'SyncedEntities' array." });
        }

        logger.LogInformation(
            "Received execute request with {Count} synced entities.",
            payload.SyncedEntities.Count);

        return new OkObjectResult(new { received = payload.SyncedEntities.Count });
    }
}
