using Microsoft.Extensions.Logging;

namespace IdvEnrichment.Functions.Shared;

public static class DownloadRetryHelper
{
    private const int MaxAttempts = 3;
    private const double MaxRetryAfterSeconds = 60;
    private const int MaxLoggedBodyChars = 2000;

    // Refusals the service will repeat identically. The document URL is an activity input, so a rejected or
    // expired URL is permanent for this activity's lifetime however many attempts it gets -- retrying only
    // spends chunk concurrency to reach the same answer. 406 is what Graph's PDF conversion service returns
    // for files it cannot convert (confirmed on a corrupt .pptx and a .docx carrying embedded PDF objects);
    // 403 is what an expired pre-authenticated download URL returns.
    private static readonly HashSet<int> PermanentRefusals = [400, 403, 404, 406, 415];

    /// <summary>
    /// Issues a download, retrying only transient failures and logging the response body on any failure.
    /// </summary>
    /// <remarks>
    /// Deliberately scoped to the HTTP call rather than the whole activity: ExtractContent submits a
    /// billable Document Intelligence job, so a whole-activity retry can resubmit work already accepted.
    /// Document Intelligence's own transient failures are handled inside the Azure SDK's retry pipeline,
    /// which retries individual requests (including each poll) rather than the logical operation.
    ///
    /// The body is logged because EnsureSuccessStatusCode discards it, and that is where a refusing service
    /// explains itself -- a bare "406" cost hours of guesswork the body would have answered directly.
    /// </remarks>
    public static Task<HttpResponseMessage> GetWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        string fileName,
        ILogger logger,
        CancellationToken ct = default)
        => GetWithRetryAsync(send, fileName, logger, Task.Delay, ct);

    // Internal overload for testability: lets tests exercise every retry path and assert the delay that
    // WOULD have been waited, without adding real sleeps to the suite.
    internal static async Task<HttpResponseMessage> GetWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        string fileName,
        ILogger logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await send(ct);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = (int)response.StatusCode;
                logger.LogWarning(
                    "Download failed for {FileName} (attempt {Attempt}/{Max}): {Status} {Reason}. "
                    + "Retry-After: {RetryAfter}. Body: {Body}",
                    fileName, attempt, MaxAttempts, status, response.ReasonPhrase,
                    response.Headers.RetryAfter?.ToString() ?? "(none)",
                    await ReadBoundedBodyAsync(response, ct));

                if (PermanentRefusals.Contains(status) || attempt == MaxAttempts)
                {
                    response.EnsureSuccessStatusCode();
                }

                var delay = GetRetryDelay(response, attempt);
                response.Dispose();
                response = null;
                await delayAsync(delay, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null && attempt < MaxAttempts)
            {
                // A null StatusCode means the request never received a response -- a connection-level
                // failure, worth another attempt. An HttpRequestException that DOES carry a status came
                // from EnsureSuccessStatusCode above and is an already-decided outcome, so it must not be
                // caught here or a permanent refusal would be retried anyway.
                response?.Dispose();
                logger.LogWarning(ex,
                    "Download of {FileName} failed to connect (attempt {Attempt}/{Max}); retrying",
                    fileName, attempt, MaxAttempts);
                await delayAsync(TimeSpan.FromSeconds(2 * attempt), ct);
            }
            catch
            {
                response?.Dispose();
                throw;
            }
        }

        throw new InvalidOperationException("Unreachable.");
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        // Retry-After can be an HTTP-date; only the delta-seconds form is handled here, matching
        // OpenAiRetryHelper.
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return TimeSpan.FromSeconds(Math.Clamp(delta.TotalSeconds, 1, MaxRetryAfterSeconds));
        }

        return TimeSpan.FromSeconds(2 * attempt);
    }

    private static async Task<string> ReadBoundedBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // A diagnostic must never become the failure, and an HTML error page would otherwise flood logs.
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            return raw.Length > MaxLoggedBodyChars ? raw[..MaxLoggedBodyChars] : raw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return "(response body could not be read)";
        }
    }
}
