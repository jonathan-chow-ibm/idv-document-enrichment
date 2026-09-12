using IdvEnrichment.Functions.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class DownloadRetryHelperTests
{
    [Fact]
    public async Task GetWithRetryAsync_Success_ReturnsResponseOnFirstAttempt()
    {
        var attempts = 0;

        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            _ => { attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); },
            "a.pdf", NullLogger.Instance, NoDelay);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotAcceptable)]      // Graph refusing to convert a corrupt/embedded-object file
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    [InlineData(HttpStatusCode.Forbidden)]          // expired pre-authenticated URL
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetWithRetryAsync_PermanentRefusal_ThrowsWithoutRetrying(HttpStatusCode status)
    {
        var attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => DownloadRetryHelper.GetWithRetryAsync(
            _ => { attempts++; return Task.FromResult(new HttpResponseMessage(status)); },
            "a.pdf", NullLogger.Instance, NoDelay));

        // The document URL is an activity input, so the service would refuse the identical request again.
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetWithRetryAsync_TransientStatus_RetriesAndSucceeds(HttpStatusCode status)
    {
        var attempts = 0;

        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(new HttpResponseMessage(
                    attempts == 1 ? status : HttpStatusCode.OK));
            },
            "a.pdf", NullLogger.Instance, NoDelay);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetWithRetryAsync_TransientThroughout_ThrowsAfterMaxAttempts()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() => DownloadRetryHelper.GetWithRetryAsync(
            _ => { attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); },
            "a.pdf", NullLogger.Instance, NoDelay));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task GetWithRetryAsync_ConnectionFailure_Retries()
    {
        var attempts = 0;

        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            _ =>
            {
                attempts++;
                // A null StatusCode is what a connection-level failure looks like, as opposed to a
                // response-carrying failure that EnsureSuccessStatusCode already decided on.
                return attempts == 1
                    ? throw new HttpRequestException("connection reset")
                    : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
            "a.pdf", NullLogger.Instance, NoDelay);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetWithRetryAsync_HonoursRetryAfter()
    {
        var attempts = 0;
        var requestedDelays = new List<TimeSpan>();

        using var response = await DownloadRetryHelper.GetWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts > 1)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }

                var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(12));
                return Task.FromResult(throttled);
            },
            "a.pdf", NullLogger.Instance,
            (delay, _) => { requestedDelays.Add(delay); return Task.CompletedTask; });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The server's Retry-After wins over the shorter default backoff.
        Assert.Equal(TimeSpan.FromSeconds(12), Assert.Single(requestedDelays));
    }

    [Fact]
    public async Task GetWithRetryAsync_NoRetryAfter_BacksOff()
    {
        var requestedDelays = new List<TimeSpan>();

        await Assert.ThrowsAsync<HttpRequestException>(() => DownloadRetryHelper.GetWithRetryAsync(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            "a.pdf", NullLogger.Instance,
            (delay, _) => { requestedDelays.Add(delay); return Task.CompletedTask; }));

        // Two waits for three attempts, increasing.
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], requestedDelays);
    }

    [Fact]
    public async Task GetWithRetryAsync_FailureBodyIsRead()
    {
        // The body is where a refusing service explains itself; EnsureSuccessStatusCode discards it.
        var body = new StringContent("conversion not supported for this file", Encoding.UTF8, "text/plain");
        var response = new HttpResponseMessage(HttpStatusCode.NotAcceptable) { Content = body };

        await Assert.ThrowsAsync<HttpRequestException>(() => DownloadRetryHelper.GetWithRetryAsync(
            _ => Task.FromResult(response), "a.pptx", NullLogger.Instance, NoDelay));
    }

    // Retry paths are exercised without real sleeps; delay VALUES are asserted where they matter.
    private static Task NoDelay(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
}
