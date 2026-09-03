using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;

namespace IdvEnrichment.Functions.Activities;

public sealed class GetDocumentDownloadUrlActivity(GraphServiceClient graphClient)
{
    [Function(nameof(GetDocumentDownloadUrl))]
    public async Task<string> GetDocumentDownloadUrl(
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
            return input.FileUrl;
        }

        var driveItem = await graphClient.Drives[input.DriveId]
            .Items[input.ItemId]
            .GetAsync(cancellationToken: ct);

        var downloadUrl = driveItem?.AdditionalData is { } data &&
            data.TryGetValue("@microsoft.graph.downloadUrl", out var urlObj)
            ? urlObj as string
            : null;

        return downloadUrl
            ?? throw new InvalidOperationException(
                $"Graph did not return a download URL for item {input.ItemId}.");
    }
}
