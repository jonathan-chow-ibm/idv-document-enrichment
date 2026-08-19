using Azure;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace IdvEnrichment.Functions.Shared;

public static class OpenAiRetryHelper
{
    private const int MaxAttempts = 3;
    private const double MaxRetryAfterSeconds = 60;

    /// <summary>Executes an OpenAI call, honouring Retry-After headers on 429s.</summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        ILogger logger,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (ex.Status == 429)
            {
                if (attempt == MaxAttempts)
                {
                    throw;
                }

                string? requestId = null;
                ex.GetRawResponse()?.Headers.TryGetValue("x-ms-client-request-id", out requestId);
                var retryAfter = GetRetryAfterSeconds(ex);
                logger.LogWarning(
                    "OpenAI rate limit hit (attempt {Attempt}/{Max}); waiting {Seconds}s (RequestId: {RequestId})",
                    attempt, MaxAttempts, retryAfter, requestId ?? "unknown");

                await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct);
            }
        }

        throw new InvalidOperationException("Unreachable.");
    }

    private static double GetRetryAfterSeconds(RequestFailedException ex)
    {
        // Retry-After can be an HTTP-date; we only handle the delta-seconds form here
        if (ex.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var value) == true
            && double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return Math.Clamp(seconds, 1, MaxRetryAfterSeconds);
        }

        return 30;
    }
}
