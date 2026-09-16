using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;
using System.ClientModel;

namespace IdvEnrichment.Functions.Shared;

// Central translation point for Graph/Azure SDK request failures inside an activity. A Durable Functions
// activity exception crosses the orchestrator boundary as an opaque TaskFailedException with none of the
// original detail, so the HTTP status or Graph error code must be captured and logged here -- before that
// boundary -- or it's lost by the time anyone is diagnosing a stalled or failed document from logs.
public static class SdkExceptionHelper
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation, string description, ILogger logger)
    {
        try
        {
            return await operation();
        }
        catch (ODataError ex)
        {
            throw Translate(ex, description, logger);
        }
        catch (RequestFailedException ex)
        {
            throw Translate(ex, description, logger);
        }
        catch (ClientResultException ex)
        {
            throw Translate(ex, description, logger);
        }
    }

    public static async Task RunAsync(Func<Task> operation, string description, ILogger logger)
    {
        try
        {
            await operation();
        }
        catch (ODataError ex)
        {
            throw Translate(ex, description, logger);
        }
        catch (RequestFailedException ex)
        {
            throw Translate(ex, description, logger);
        }
        catch (ClientResultException ex)
        {
            throw Translate(ex, description, logger);
        }
    }

    private static InvalidOperationException Translate(ODataError ex, string description, ILogger logger)
    {
        var code = ex.Error?.Code ?? "unknown";
        var message = ex.Error?.Message ?? ex.Message;
        logger.LogError(ex, "{Description} failed: Graph error {Code} - {Message}", description, code, message);
        return new InvalidOperationException($"{description} failed: Graph error {code} - {message}", ex);
    }

    private static InvalidOperationException Translate(RequestFailedException ex, string description, ILogger logger)
    {
        logger.LogError(ex, "{Description} failed: HTTP {Status} - {Message}", description, ex.Status, ex.Message);
        return new InvalidOperationException($"{description} failed: HTTP {ex.Status} - {ex.Message}", ex);
    }

    private static InvalidOperationException Translate(ClientResultException ex, string description, ILogger logger)
    {
        logger.LogError(ex, "{Description} failed: HTTP {Status} - {Message}", description, ex.Status, ex.Message);
        return new InvalidOperationException($"{description} failed: HTTP {ex.Status} - {ex.Message}", ex);
    }
}
