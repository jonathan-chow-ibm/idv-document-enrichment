using IdvEnrichment.Functions.Models;
using IdvEnrichment.Functions.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace IdvEnrichment.Functions.Activities;

public sealed class ResolveSharePointTargetActivity(GraphServiceClient graphClient, ILogger<ResolveSharePointTargetActivity> logger)
{
    [Function(nameof(ResolveSharePointTarget))]
    public async Task<ResolvedSharePointTarget> ResolveSharePointTarget(
        [ActivityTrigger] string url,
        CancellationToken ct = default)
    {
        var (hostname, sitePath, libraryName, folderPath) = ParseSharePointUrl(url);

        var site = await SdkExceptionHelper.RunAsync(
            () => graphClient.Sites[$"{hostname}:{sitePath}"].GetAsync(cancellationToken: ct),
            $"Resolving SharePoint site {hostname}:{sitePath}",
            logger)
            ?? throw new InvalidOperationException($"Could not find SharePoint site at {hostname}:{sitePath}");

        var siteId = site.Id!;
        var siteName = site.DisplayName ?? site.Name ?? hostname;

        var drivesResponse = await SdkExceptionHelper.RunAsync(
            () => graphClient.Sites[siteId].Drives.GetAsync(cancellationToken: ct),
            $"Listing drives for site {siteName}",
            logger);
        var drives = drivesResponse?.Value ?? [];

        var drive = SelectDrive(drives, libraryName, siteName);

        return new ResolvedSharePointTarget(
            SiteId: siteId,
            DriveId: drive.Id!,
            FolderPath: folderPath,
            SiteName: siteName,
            LibraryName: drive.Name!);
    }

    internal static (string Hostname, string SitePath, string LibraryName, string? FolderPath) ParseSharePointUrl(string url)
    {
        var uri = new Uri(url);
        var hostname = uri.Host;

        // The `id` query parameter (set by SharePoint when browsing library subfolders in the UI)
        // is the authoritative server-relative path — prefer it when present.
        var idParam = GetQueryParameter(uri.Query, "id");

        string[] segments;
        if (!string.IsNullOrWhiteSpace(idParam))
        {
            // GetQueryParameter already fully decoded idParam — split only, without a second decode pass
            // (a folder name containing a literal "%25" would otherwise be misread as "%").
            segments = idParam!.Split('/', StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            // uri.AbsolutePath is percent-encoded — split then decode each segment.
            segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
        }

        // Reject SharePoint/OneDrive "Copy link" share URLs (e.g. https://tenant.sharepoint.com/:f:/s/SiteName/Ev1abc...).
        // The leading ":x:" segment (any single-character discriminator: f/w/x/b/p/o/v/i/t/u/...) carries a sharing
        // token, not a real site path, and would otherwise fail confusingly during the site lookup.
        if (segments.Length > 0 && segments[0] is [':', _, ':'])
        {
            throw new ArgumentException(
                "This looks like a SharePoint 'Copy link' share URL, which isn't supported. " +
                "Please copy the URL directly from your browser's address bar while viewing the folder instead. " +
                $"URL: {url}");
        }

        // Strip a trailing `Forms/<something>.aspx` pair added by the SharePoint browser UI.
        if (segments.Length >= 2 &&
            string.Equals(segments[^2], "Forms", StringComparison.OrdinalIgnoreCase) &&
            segments[^1].EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
        {
            segments = segments[..^2];
        }

        if (segments.Length < 3)
        {
            throw new ArgumentException(
                $"SharePoint URL must include a site and library (e.g. https://tenant.sharepoint.com/sites/SiteName/LibraryName): {url}");
        }

        var sitePath = $"/{segments[0]}/{segments[1]}";
        var libraryName = segments[2];
        var folderPath = segments.Length > 3 ? string.Join("/", segments.Skip(3)) : null;

        return (hostname, sitePath, libraryName, folderPath);
    }

    // Match on the drive's WebUrl path suffix, not display Name. SharePoint guarantees URL slugs are
    // unique per site (special characters like "-" get stripped/collision-suffixed at creation), so
    // WebUrl matching can never be ambiguous — whereas Name matching can silently pick the WRONG
    // library when one drive's URL-derived segment happens to equal a DIFFERENT drive's real display
    // Name (e.g. a slug collision auto-suffixed with "1"). See incident: "Foo"/"Foo-" pair where "Foo-"'s
    // URL segment is "Foo" (dash stripped), which exactly matched an unrelated "Foo" library's Name.
    internal static Drive SelectDrive(IReadOnlyList<Drive> drives, string libraryName, string siteName)
    {
        return drives.FirstOrDefault(d => DriveWebUrlEndsWithSegment(d.WebUrl, libraryName))
            ?? throw new InvalidOperationException(
                $"Library '{libraryName}' not found in site '{siteName}'. " +
                $"Available libraries: {string.Join(", ", drives.Select(d => d.Name))}");
    }

    private static bool DriveWebUrlEndsWithSegment(string? webUrl, string segment)
    {
        if (string.IsNullOrEmpty(webUrl) || !Uri.TryCreate(webUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var decodedPath = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        return decodedPath.EndsWith("/" + segment, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetQueryParameter(string query, string name)
    {
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = eq < 0 ? pair : pair[..eq];
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                var value = eq < 0 ? string.Empty : pair[(eq + 1)..];
                return Uri.UnescapeDataString(value);
            }
        }

        return null;
    }
}
