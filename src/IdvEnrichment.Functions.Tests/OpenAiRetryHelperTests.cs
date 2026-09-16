using Azure;
using IdvEnrichment.Functions.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using System.ClientModel;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class OpenAiRetryHelperTests
{
    private static readonly NullLogger<object> Logger = NullLogger<object>.Instance;

    [Fact]
    public async Task ExecuteWithRetryAsync_ClientResultException429_RetriesThenSucceeds()
    {
        var attempts = 0;

        var result = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw FakeClientResultException.Create(429, new Dictionary<string, string> { ["Retry-After"] = "1" });
                }

                return Task.FromResult(42);
            },
            Logger, NoDelay);

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ClientResultException429_ExhaustsRetriesThenRethrows()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<ClientResultException>(() =>
            OpenAiRetryHelper.ExecuteWithRetryAsync<int>(
                _ =>
                {
                    attempts++;
                    throw FakeClientResultException.Create(429, new Dictionary<string, string> { ["Retry-After"] = "1" });
                },
                Logger, NoDelay));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ClientResultExceptionNon429_NotRetried()
    {
        var attempts = 0;

        // 400 is a permanent client error (bad request), unlike the transient 5xx statuses that are
        // now retryable alongside 429 — see ExecuteWithRetryAsync_ClientResultException5xx_RetriesThenSucceeds.
        await Assert.ThrowsAsync<ClientResultException>(() =>
            OpenAiRetryHelper.ExecuteWithRetryAsync<int>(
                _ =>
                {
                    attempts++;
                    throw FakeClientResultException.Create(400);
                },
                Logger, NoDelay));

        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task ExecuteWithRetryAsync_ClientResultException5xx_RetriesThenSucceeds(int status)
    {
        var attempts = 0;

        var result = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw FakeClientResultException.Create(status);
                }

                return Task.FromResult(99);
            },
            Logger, NoDelay);

        Assert.Equal(99, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RequestFailedException429_RetriesThenSucceeds()
    {
        var attempts = 0;

        var result = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new RequestFailedException(429, "Throttled");
                }

                return Task.FromResult(7);
            },
            Logger, NoDelay);

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task ExecuteWithRetryAsync_RequestFailedException5xx_RetriesThenSucceeds(int status)
    {
        var attempts = 0;

        var result = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new RequestFailedException(status, "Service unavailable");
                }

                return Task.FromResult(3);
            },
            Logger, NoDelay);

        Assert.Equal(3, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_RequestFailedExceptionNon429_NotRetried()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            OpenAiRetryHelper.ExecuteWithRetryAsync<int>(
                _ =>
                {
                    attempts++;
                    throw new RequestFailedException(404, "Not found");
                },
                Logger, NoDelay));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_NoRetryAfterHeader_DefaultsTo30Seconds()
    {
        var attempts = 0;
        var requestedDelays = new List<TimeSpan>();

        // Mirrors the real bug this seam fixes: RequestFailedException(429, "Throttled") never attaches
        // a Response, so GetRawResponse() is null and the helper falls through to its 30s default delay.
        var result = await OpenAiRetryHelper.ExecuteWithRetryAsync(
            _ =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new RequestFailedException(429, "Throttled");
                }

                return Task.FromResult(1);
            },
            Logger,
            (delay, _) => { requestedDelays.Add(delay); return Task.CompletedTask; });

        Assert.Equal(1, result);
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(requestedDelays));
    }

    // Retry paths are exercised without real sleeps; delay VALUES are asserted where they matter.
    private static Task NoDelay(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
}
