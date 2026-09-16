using Azure;
using IdvEnrichment.Functions.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Models.ODataErrors;
using System.ClientModel;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class SdkExceptionHelperTests
{
    private static readonly NullLogger<object> Logger = NullLogger<object>.Instance;

    [Fact]
    public async Task RunAsync_Generic_ODataError_TranslatesWithCodeAndMessage()
    {
        var oDataError = new ODataError
        {
            Error = new MainError { Code = "activityLimitReached", Message = "Too many requests" },
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync<int>(
                () => throw oDataError,
                "Listing drive items",
                Logger));

        Assert.Contains("Listing drive items", ex.Message);
        Assert.Contains("activityLimitReached", ex.Message);
        Assert.Contains("Too many requests", ex.Message);
        Assert.Same(oDataError, ex.InnerException);
    }

    [Fact]
    public async Task RunAsync_Generic_RequestFailedException_TranslatesWithStatusAndMessage()
    {
        var requestFailed = new RequestFailedException(503, "Service unavailable");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync<int>(
                () => throw requestFailed,
                "Uploading batch report",
                Logger));

        Assert.Contains("Uploading batch report", ex.Message);
        Assert.Contains("503", ex.Message);
        Assert.Contains("Service unavailable", ex.Message);
        Assert.Same(requestFailed, ex.InnerException);
    }

    [Fact]
    public async Task RunAsync_NonGeneric_ODataError_TranslatesWithCodeAndMessage()
    {
        var oDataError = new ODataError
        {
            Error = new MainError { Code = "itemNotFound", Message = "The resource could not be found." },
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync(
                () => throw oDataError,
                "Processed-document lookup",
                Logger));

        Assert.Contains("Processed-document lookup", ex.Message);
        Assert.Contains("itemNotFound", ex.Message);
        Assert.Contains("The resource could not be found.", ex.Message);
    }

    [Fact]
    public async Task RunAsync_NonGeneric_RequestFailedException_TranslatesWithStatusAndMessage()
    {
        var requestFailed = new RequestFailedException(429, "Throttled");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync(
                () => throw requestFailed,
                "Batch report write",
                Logger));

        Assert.Contains("Batch report write", ex.Message);
        Assert.Contains("429", ex.Message);
        Assert.Contains("Throttled", ex.Message);
    }

    [Fact]
    public async Task RunAsync_Generic_ClientResultException_TranslatesWithStatusAndMessage()
    {
        var clientResultException = FakeClientResultException.Create(429);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync<int>(
                () => throw clientResultException,
                "Classifying document",
                Logger));

        Assert.Contains("Classifying document", ex.Message);
        Assert.Contains("429", ex.Message);
        Assert.Same(clientResultException, ex.InnerException);
    }

    [Fact]
    public async Task RunAsync_NonGeneric_ClientResultException_TranslatesWithStatusAndMessage()
    {
        var clientResultException = FakeClientResultException.Create(500);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SdkExceptionHelper.RunAsync(
                () => throw clientResultException,
                "Extracting drawing details",
                Logger));

        Assert.Contains("Extracting drawing details", ex.Message);
        Assert.Contains("500", ex.Message);
        Assert.Same(clientResultException, ex.InnerException);
    }
}
