using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace IdvEnrichment.Functions.Activities;

public sealed class EnumerateLibraryActivity(GraphServiceClient graphClient)
{
    private static readonly string[] SelectFields =
    [
        "id", "name", "file", "folder", "lastModifiedDateTime"
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
        // Pre-check only the top-level folder path — a SharePoint `id` query param can resolve to a selected
        // file rather than a folder, and calling .Children on a file surfaces an unhelpful Graph error. Recursive
        // calls below are gated on `item.Folder is not null` from the parent listing, so they need no re-check.
        if (target.FolderPath is not null)
        {
            DriveItem? item;
            try
            {
                item = await graphClient.Drives[target.DriveId]
                    .Items[$"root:/{target.FolderPath}:"]
                    .GetAsync(cancellationToken: ct);
            }
            catch (ODataError ex) when (string.Equals(ex.Error?.Code, "itemNotFound", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The folder path '{target.FolderPath}' was not found in library '{target.LibraryName}'. " +
                    "Check the SharePoint URL for typos.");
            }

            if (item is null || item.Folder is null)
            {
                throw new InvalidOperationException(
                    $"The resolved path '{target.FolderPath}' is a file, not a folder — " +
                    "check the SharePoint URL you provided points to a folder, not a specific document.");
            }
        }

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
                    documents.Add(new LibraryDocument(
                        Id: item.Id!,
                        Name: item.Name!,
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
