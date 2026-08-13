using IdvEnrichment.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Graph;

namespace IdvEnrichment.Functions.Activities;

public sealed class ResolveSharePointTargetActivity(GraphServiceClient graphClient)
{
    [Function(nameof(ResolveSharePointTarget))]
    public async Task<ResolvedSharePointTarget> ResolveSharePointTarget(
        [ActivityTrigger] string url,
        CancellationToken ct = default)
    {
        var (hostname, sitePath, libraryName, folderPath) = ParseSharePointUrl(url);

        var site = await graphClient.Sites[$"{hostname}:{sitePath}"].GetAsync(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Could not find SharePoint site at {hostname}:{sitePath}");

        var siteId = site.Id!;
        var siteName = site.DisplayName ?? site.Name ?? hostname;

        var drivesResponse = await graphClient.Sites[siteId].Drives.GetAsync(cancellationToken: ct);
        var drives = drivesResponse?.Value ?? [];

        var drive = drives.FirstOrDefault(d => string.Equals(d.Name, libraryName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Library '{libraryName}' not found in site '{siteName}'. " +
                $"Available libraries: {string.Join(", ", drives.Select(d => d.Name))}");

        return new ResolvedSharePointTarget(
            SiteId: siteId,
            DriveId: drive.Id!,
            FolderPath: folderPath,
            SiteName: siteName,
            LibraryName: drive.Name!);
    }

    private static (string Hostname, string SitePath, string LibraryName, string? FolderPath) ParseSharePointUrl(string url)
    {
        var uri = new Uri(url);
        var hostname = uri.Host;
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (segments.Length < 3)
        {
            throw new ArgumentException(
                $"SharePoint URL must include a site and library (e.g. https://tenant.sharepoint.com/sites/SiteName/LibraryName): {url}");
        }

        // segments: ["sites", "ActiveProjects", "Shared Documents", "2024"]
        var sitePath = $"/{segments[0]}/{segments[1]}";
        var libraryName = segments[2];
        var folderPath = segments.Length > 3 ? string.Join("/", segments.Skip(3)) : null;

        return (hostname, sitePath, libraryName, folderPath);
    }
}
