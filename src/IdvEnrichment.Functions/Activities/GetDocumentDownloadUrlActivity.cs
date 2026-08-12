using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;

namespace IdvEnrichment.Functions.Activities;

public sealed class GetDocumentDownloadUrlActivity(GraphServiceClient graphClient)
{
    [Function(nameof(GetDocumentDownloadUrl))]
    public async Task<string> GetDocumentDownloadUrl(
        [ActivityTrigger] QueueMessage message,
        CancellationToken ct = default)
    {
        var driveItem = await graphClient.Drives[message.DriveId]
            .Items[message.ItemId]
            .GetAsync(
                config => config.QueryParameters.Select = ["id", "@microsoft.graph.downloadUrl"],
                ct);

        var downloadUrl = driveItem?.AdditionalData
            .TryGetValue("@microsoft.graph.downloadUrl", out var urlObj) == true
            ? urlObj as string
            : null;

        return downloadUrl
            ?? throw new InvalidOperationException(
                $"Graph did not return a download URL for item {message.ItemId}.");
    }
}
