using Azure.Core;
using IdvEnrichment.Functions.Activities;
using IdvEnrichment.Functions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Net;
using System.Text;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class GetDocumentDownloadUrlActivityTests
{
    [Theory]
    [InlineData("report.docx", true)]
    [InlineData("slides.pptx", true)]
    [InlineData("REPORT.DOCX", true)]
    [InlineData("drawing.pdf", false)]
    [InlineData("workbook.xlsx", false)]
    [InlineData("notes.txt", false)]
    public void ShouldConvertToPdf_ReturnsExpected(string fileName, bool expected)
    {
        var result = GetDocumentDownloadUrlActivity.ShouldConvertToPdf(fileName);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task TryConvertToPdfAsync_Redirect_ReturnsLocationAndStatus()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri("https://example.blob.core.windows.net/converted.pdf");
        var activity = CreateActivity(new StubHttpMessageHandler(response));

        var result = await activity.TryConvertToPdfAsync("drive-1", "item-1", CancellationToken.None);

        Assert.Equal("https://example.blob.core.windows.net/converted.pdf", result.Url);
        Assert.Equal(HttpStatusCode.Found, result.StatusCode);
    }

    [Fact]
    public async Task TryConvertToPdfAsync_NoRedirect_ReturnsNullUrlWithStatus()
    {
        // Anything other than a redirect means no conversion happened -- the status has to survive so the
        // caller can log why, instead of silently falling back to the unconverted file.
        var activity = CreateActivity(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var result = await activity.TryConvertToPdfAsync("drive-1", "item-1", CancellationToken.None);

        Assert.Null(result.Url);
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task TryConvertToPdfAsync_SendsBearerTokenToFormatPdfEndpoint()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Found));
        var activity = CreateActivity(handler);

        await activity.TryConvertToPdfAsync("drive-1", "item-1", CancellationToken.None);

        Assert.Equal(
            "https://graph.microsoft.com/v1.0/drives/drive-1/items/item-1/content?format=pdf",
            handler.CapturedRequestUri);
        Assert.Equal("Bearer stub-token", handler.CapturedAuthorization);
    }

    [Fact]
    public async Task GetDocumentDownloadUrl_PreAuthenticatedNonSharePointUrl_ReturnsUrlUnchanged()
    {
        // Returns before Graph is reached, so the .docx name never gets a chance to trigger conversion.
        var activity = CreateActivity(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Found)));

        var result = await activity.GetDocumentDownloadUrl(
            new GetDocumentDownloadUrlInput("drive-1", "item-1", "https://example.com/report.docx", "report.docx"));

        Assert.Equal("https://example.com/report.docx", result.Url);
        Assert.False(result.IsConvertedToPdf);
    }

    [Fact]
    public async Task GetDocumentDownloadUrl_OfficeFile_ConversionRedirect_ReturnsConvertedPdf()
    {
        var conversion = new HttpResponseMessage(HttpStatusCode.Found);
        conversion.Headers.Location = new Uri("https://example.blob.core.windows.net/converted.pdf");
        var activity = CreateActivity(new StubHttpMessageHandler(conversion), DriveItemJson);

        var result = await activity.GetDocumentDownloadUrl(
            new GetDocumentDownloadUrlInput("drive-1", "item-1", string.Empty, "report.docx"));

        Assert.Equal("https://example.blob.core.windows.net/converted.pdf", result.Url);
        Assert.True(result.IsConvertedToPdf);
        // The unconverted file travels with it: the media service refuses some files only when the
        // converted URL is fetched, which happens two activities later, so extraction needs a fallback.
        Assert.Equal(OriginalDownloadUrl, result.OriginalUrl);
    }

    [Fact]
    public async Task GetDocumentDownloadUrl_ConversionWithoutRedirect_FallsBackToOriginalFile()
    {
        var activity = CreateActivity(
            new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)), DriveItemJson);

        var result = await activity.GetDocumentDownloadUrl(
            new GetDocumentDownloadUrlInput("drive-1", "item-1", string.Empty, "report.docx"));

        Assert.Equal(OriginalDownloadUrl, result.Url);
        Assert.False(result.IsConvertedToPdf);
        // Nothing to fall back FROM when the URL is already the original.
        Assert.Null(result.OriginalUrl);
    }

    [Fact]
    public async Task GetDocumentDownloadUrl_NonConvertibleFile_ReturnsGraphDownloadUrl()
    {
        // A .pdf is already in the target format, so no conversion request should be attempted at all.
        var conversionHandler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Found));
        var activity = CreateActivity(conversionHandler, DriveItemJson);

        var result = await activity.GetDocumentDownloadUrl(
            new GetDocumentDownloadUrlInput("drive-1", "item-1", string.Empty, "drawing.pdf"));

        Assert.Equal(OriginalDownloadUrl, result.Url);
        Assert.False(result.IsConvertedToPdf);
        Assert.Null(conversionHandler.CapturedRequestUri);
    }

    private const string OriginalDownloadUrl = "https://contoso.sharepoint.com/original.docx";

    private static readonly string DriveItemJson =
        $$"""{"id":"item-1","name":"report.docx","@microsoft.graph.downloadUrl":"{{OriginalDownloadUrl}}"}""";

    // conversionHandler serves the format=pdf call; driveItemJson, when supplied, is what Graph answers
    // the drive-item lookup with. Tests that never reach Graph leave it null.
    private static GetDocumentDownloadUrlActivity CreateActivity(
        HttpMessageHandler conversionHandler,
        string? driveItemJson = null)
    {
        var credential = new StubTokenCredential();

        var graphClient = driveItemJson is null
            ? new GraphServiceClient(credential)
            : new GraphServiceClient(
                new HttpClient(new StubHttpMessageHandler(JsonResponse(driveItemJson))),
                new AnonymousAuthenticationProvider());

        return new GetDocumentDownloadUrlActivity(
            graphClient,
            new SingleClientHttpClientFactory(conversionHandler),
            credential,
            NullLogger<GetDocumentDownloadUrlActivity>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // Captures the outgoing request eagerly -- the activity disposes it before a test could inspect it.
    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? CapturedRequestUri { get; private set; }
        public string? CapturedAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CapturedRequestUri = request.RequestUri?.ToString();
            CapturedAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(response);
        }
    }

    private sealed class SingleClientHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken ct) =>
            new("stub-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken ct) =>
            new(GetToken(requestContext, ct));
    }
}
