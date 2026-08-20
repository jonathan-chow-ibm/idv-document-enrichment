using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace IdvEnrichment.Functions.Activities;

public sealed class EnumerateLibraryActivity(GraphServiceClient graphClient)
{
    private static readonly string[] SelectFields =
    [
        "id", "name", "file", "folder", "lastModifiedDateTime", "@microsoft.graph.downloadUrl"
    ];

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xlsm", ".pptx", ".txt", ".png", ".jpg", ".jpeg", ".msg"
    };

    [Function(nameof(EnumerateLibrary))]
    public async Task<IReadOnlyList<LibraryDocument>> EnumerateLibrary(
        [ActivityTrigger] ResolvedSharePointTarget target,
        CancellationToken ct = default)
    {
        var documents = new List<LibraryDocument>();
        await CollectDocumentsAsync(target.DriveId, target.FolderPath, documents, ct);
        return documents;
    }

    private async Task CollectDocumentsAsync(
        string driveId,
        string? folderPath,
        List<LibraryDocument> documents,
        CancellationToken ct)
    {
        DriveItemCollectionResponse? response;
        if (folderPath is null)
        {
            response = await graphClient.Drives[driveId].Items["root"].Children
                .GetAsync(cfg => cfg.QueryParameters.Select = SelectFields, ct);
        }
        else
        {
            response = await graphClient.Drives[driveId].Items[$"root:/{folderPath}:"].Children
                .GetAsync(cfg => cfg.QueryParameters.Select = SelectFields, ct);
        }

        if (response is null)
        {
            return;
        }

        var subfolderPaths = new List<string>();

        var pageIterator = PageIterator<DriveItem, DriveItemCollectionResponse>
            .CreatePageIterator(graphClient, response, item =>
            {
                if (item.Folder is not null)
                {
                    var subPath = folderPath is null ? item.Name! : $"{folderPath}/{item.Name}";
                    subfolderPaths.Add(subPath);
                }
                else if (SupportedExtensions.Contains(Path.GetExtension(item.Name ?? "")))
                {
                    var downloadUrl = item.AdditionalData?.TryGetValue("@microsoft.graph.downloadUrl", out var urlObj) == true
                        ? urlObj as string ?? string.Empty
                        : string.Empty;

                    documents.Add(new LibraryDocument(
                        Id: item.Id!,
                        Name: item.Name!,
                        DownloadUrl: downloadUrl,
                        MimeType: item.File?.MimeType ?? "application/octet-stream",
                        LastModifiedDateTime: item.LastModifiedDateTime ?? DateTimeOffset.MinValue,
                        FolderPath: folderPath));
                }

                return true;
            });

        await pageIterator.IterateAsync(ct);

        foreach (var subfolder in subfolderPaths)
        {
            await CollectDocumentsAsync(driveId, subfolder, documents, ct);
        }
    }
}
