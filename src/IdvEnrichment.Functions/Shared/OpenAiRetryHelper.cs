using Azure;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.Globalization;

namespace IdvEnrichment.Functions.Shared;

public static class OpenAiRetryHelper
{
    private const int MaxAttempts = 3;
    private const double MaxRetryAfterSeconds = 60;
    private const double DefaultRetryAfterSeconds = 30;

    // 429 (rate limit) and the usual transient cloud statuses. 408/502/503/504 are the ones a load
    // balancer or the model backend itself throws under transient pressure -- they carry no Durable-level
    // retry anymore (DocumentOrchestrator.cs), so this helper is now the only thing protecting against them.
    private static readonly HashSet<int> RetryableStatuses = [408, 429, 500, 502, 503, 504];

    // Bounds each individual call attempt. Without this, a stalled connection or unresponsive endpoint
    // hangs indefinitely — retries never engage because they only trigger on a thrown exception, and a
    // hung call never throws. A live batch run stalled for hours on exactly this before this fix existed.
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Executes an OpenAI call, retrying transient statuses (429/408/5xx) with Retry-After-aware
    /// backoff and bounding each attempt to <see cref="CallTimeout"/> so a hung call is treated as a
    /// retryable failure instead of hanging forever.</summary>
    public static Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        CancellationToken ct = default)
        => ExecuteWithRetryAsync(operation, logger, Task.Delay, ct);

    // Internal overload for testability: lets tests exercise every retry path and assert the delay that
    // WOULD have been waited, without adding real sleeps to the suite.
    internal static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
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
            catch (RequestFailedException ex) when (RetryableStatuses.Contains(ex.Status))
            {
                if (attempt == MaxAttempts)
                {
                    throw;
                }

                string? requestId = null;
                ex.GetRawResponse()?.Headers.TryGetValue("x-ms-client-request-id", out requestId);
                var retryAfter = GetRetryAfterSeconds(ex);
                logger.LogWarning(
                    "OpenAI call failed with status {Status} (attempt {Attempt}/{Max}); waiting {Seconds}s (RequestId: {RequestId})",
                    ex.Status, attempt, MaxAttempts, retryAfter, requestId ?? "unknown");

                await delayAsync(TimeSpan.FromSeconds(retryAfter), ct);
            }
            catch (ClientResultException ex) when (RetryableStatuses.Contains(ex.Status))
            {
                // The OpenAI SDK (System.ClientModel) throws ClientResultException, not RequestFailedException --
                // an unrelated sibling type with the same Status/GetRawResponse shape. Without this catch, a 429
                // or 5xx from the chat completion call bypassed retry entirely and failed the whole activity.
                if (attempt == MaxAttempts)
                {
                    throw;
                }

                string? requestId = null;
                ex.GetRawResponse()?.Headers.TryGetValue("x-ms-client-request-id", out requestId);
                var retryAfter = GetRetryAfterSeconds(ex);
                logger.LogWarning(
                    "OpenAI call failed with status {Status} (attempt {Attempt}/{Max}); waiting {Seconds}s (RequestId: {RequestId})",
                    ex.Status, attempt, MaxAttempts, retryAfter, requestId ?? "unknown");

                await delayAsync(TimeSpan.FromSeconds(retryAfter), ct);
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
