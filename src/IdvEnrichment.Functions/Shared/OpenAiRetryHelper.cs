using Azure;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.Globalization;

namespace IdvEnrichment.Functions.Shared;

public static class OpenAiRetryHelper
{
    private const int MaxAttempts = 3;
    private const double MaxRetryAfterSeconds = 60;

    // Bounds each individual call attempt. Without this, a stalled connection or unresponsive endpoint
    // hangs indefinitely — retries never engage because they only trigger on a thrown exception, and a
    // hung call never throws. A live batch run stalled for hours on exactly this before this fix existed.
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Executes an OpenAI call, honouring Retry-After headers on 429s and bounding each attempt
    /// to <see cref="CallTimeout"/> so a hung call is treated as a retryable failure instead of hanging forever.</summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CallTimeout);

            try
            {
                return await operation(timeoutCts.Token);
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
            catch (ClientResultException ex) when (ex.Status == 429)
            {
                // The OpenAI SDK (System.ClientModel) throws ClientResultException, not RequestFailedException --
                // an unrelated sibling type with the same Status/GetRawResponse shape. Without this catch, a 429
                // from the chat completion call bypassed retry entirely and failed the whole activity.
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
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // timeoutCts fired, not the caller's own token — treat as a retryable, bounded failure.
                if (attempt == MaxAttempts)
                {
                    throw new TimeoutException(
                        $"OpenAI call did not complete within {CallTimeout.TotalSeconds}s (attempt {attempt}/{MaxAttempts}).");
                }

                logger.LogWarning(
                    "OpenAI call timed out after {Seconds}s (attempt {Attempt}/{Max}); retrying",
                    CallTimeout.TotalSeconds, attempt, MaxAttempts);
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

    private static double GetRetryAfterSeconds(ClientResultException ex)
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
