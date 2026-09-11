using Azure.Core;
using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System.Net;
using System.Net.Http.Headers;

namespace IdvEnrichment.Functions.Activities;

public sealed class GetDocumentDownloadUrlActivity(
    GraphServiceClient graphClient,
    IHttpClientFactory httpClientFactory,
    TokenCredential tokenCredential,
    ILogger<GetDocumentDownloadUrlActivity> logger)
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    [Function(nameof(GetDocumentDownloadUrl))]
    public async Task<DocumentDownloadResult> GetDocumentDownloadUrl(
        [ActivityTrigger] GetDocumentDownloadUrlInput input,
        CancellationToken ct = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input), "GetDocumentDownloadUrl: activity input deserialized as null.");
        }

        // Use FileUrl directly if it's a pre-authenticated URL (non-SharePoint) — avoids Graph call for URLs already accessible
        if (input.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !input.FileUrl.Contains("sharepoint.com", StringComparison.OrdinalIgnoreCase))
        {
            return new DocumentDownloadResult(input.FileUrl, IsConvertedToPdf: false);
        }

        var driveItem = await graphClient.Drives[input.DriveId]
            .Items[input.ItemId]
            .GetAsync(cancellationToken: ct);

        var downloadUrl = driveItem?.AdditionalData is { } data &&
            data.TryGetValue("@microsoft.graph.downloadUrl", out var urlObj)
            ? urlObj as string
            : null;

        if (downloadUrl is null)
        {
            throw new InvalidOperationException(
                $"Graph did not return a download URL for item {input.ItemId}.");
        }

        if (!ShouldConvertToPdf(input.FileName))
        {
            return new DocumentDownloadResult(downloadUrl, IsConvertedToPdf: false);
        }

        // A Word/PowerPoint file converted to PDF always has a real text layer (never a scan), so it
        // will be classified as born-digital and extracted for free by PdfPig instead of paying for a
        // Document Intelligence call. Any failure here falls back to the original file, unconverted.
        try
        {
            var conversion = await TryConvertToPdfAsync(input.DriveId, input.ItemId, ct);
            if (!string.IsNullOrEmpty(conversion.Url))
            {
                return new DocumentDownloadResult(conversion.Url, IsConvertedToPdf: true);
            }

            // Graph answered but didn't hand back the redirect the conversion is read from. Staying
            // silent here would let a tenant-wide breakage (permissions, licensing, API deprecation)
            // quietly revert every Office document to full Document Intelligence cost with no signal
            // that it happened.
            logger.LogWarning(
                "Graph PDF conversion for item {ItemId} returned {StatusCode} with no redirect location; falling back to the original file.",
                input.ItemId, conversion.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Graph PDF conversion failed for item {ItemId}; falling back to the original file.", input.ItemId);
        }

        return new DocumentDownloadResult(downloadUrl, IsConvertedToPdf: false);
    }

    // Url is null unless Graph answered with the 302 the converted file is served from; StatusCode is
    // carried out so a missing redirect can be logged with the reason instead of failing silently.
    internal readonly record struct PdfConversionAttempt(string? Url, HttpStatusCode StatusCode);

    internal async Task<PdfConversionAttempt> TryConvertToPdfAsync(string driveId, string itemId, CancellationToken ct)
    {
        var token = await tokenCredential.GetTokenAsync(new TokenRequestContext(GraphScopes), ct);

        var client = httpClientFactory.CreateClient("graph-no-redirect");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/drives/{driveId}/items/{itemId}/content?format=pdf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var response = await client.SendAsync(request, ct);

        return new PdfConversionAttempt(response.Headers.Location?.ToString(), response.StatusCode);
    }

    // Only .docx/.pptx are reachable today per EnumerateLibraryActivity's SupportedExtensions allow-list,
    // but this covers Graph's full documented format=pdf source list since it costs nothing to future-proof.
    internal static bool ShouldConvertToPdf(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() is ".doc" or ".docx" or ".dot" or ".dotx" or ".dotm"
            or ".ppt" or ".pptx" or ".pps" or ".ppsx";
}

