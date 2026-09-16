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
            Logger);

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
                Logger));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ClientResultExceptionNon429_NotRetried()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<ClientResultException>(() =>
            OpenAiRetryHelper.ExecuteWithRetryAsync<int>(
                _ =>
                {
                    attempts++;
                    throw FakeClientResultException.Create(500);
                },
                Logger));

        Assert.Equal(1, attempts);
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
            Logger);

        Assert.Equal(7, result);
        Assert.Equal(2, attempts);
    }
}
