using IdvEnrichment.Functions.Activities;
using Microsoft.Graph.Models;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class ResolveSharePointTargetActivityTests
{
    [Fact]
    public void ParseSharePointUrl_PlainSiteAndLibrary_ReturnsNullFolderPath()
    {
        var (hostname, sitePath, libraryName, folderPath) = ResolveSharePointTargetActivity.ParseSharePointUrl(
            "https://tenant.sharepoint.com/sites/SiteName/LibraryName");

        Assert.Equal("tenant.sharepoint.com", hostname);
        Assert.Equal("/sites/SiteName", sitePath);
        Assert.Equal("LibraryName", libraryName);
        Assert.Null(folderPath);
    }

    [Fact]
    public void ParseSharePointUrl_PlainUrlWithFolder_ReturnsFolderPath()
    {
        var (_, sitePath, libraryName, folderPath) = ResolveSharePointTargetActivity.ParseSharePointUrl(
            "https://tenant.sharepoint.com/sites/SiteName/LibraryName/sub/folder");

        Assert.Equal("/sites/SiteName", sitePath);
        Assert.Equal("LibraryName", libraryName);
        Assert.Equal("sub/folder", folderPath);
    }

    [Fact]
    public void ParseSharePointUrl_FormsAllItemsAspxNoQueryString_StripsAndReturnsNullFolderPath()
    {
        var (hostname, sitePath, libraryName, folderPath) = ResolveSharePointTargetActivity.ParseSharePointUrl(
            "https://idvllc.sharepoint.com/sites/IDVProjects/Test/Forms/AllItems.aspx");

        Assert.Equal("idvllc.sharepoint.com", hostname);
        Assert.Equal("/sites/IDVProjects", sitePath);
        Assert.Equal("Test", libraryName);
        Assert.Null(folderPath);
    }

    [Fact]
    public void ParseSharePointUrl_FormsAllItemsAspxWithIdQueryParam_UsesIdAsAuthoritativePath()
    {
        var (hostname, sitePath, libraryName, folderPath) = ResolveSharePointTargetActivity.ParseSharePointUrl(
            "https://idvllc.sharepoint.com/sites/IDVProjects/Test/Forms/AllItems.aspx" +
            "?id=%2Fsites%2FIDVProjects%2FTest%2FBatchTest%2DSubset%2FDrawings" +
            "&viewid=c10f62b8-52fb-4f92-9dec-170925bdd379");

        Assert.Equal("idvllc.sharepoint.com", hostname);
        Assert.Equal("/sites/IDVProjects", sitePath);
        Assert.Equal("Test", libraryName);
        Assert.Equal("BatchTest-Subset/Drawings", folderPath);
    }

    [Fact]
    public void ParseSharePointUrl_IdQueryParamWithLiteralPercentEncoding_DoesNotSecondDecode()
    {
        // The `id` query parameter contains an already-decoded folder name that itself contains a literal "%25"
        // (i.e. a percent sign in the folder name — which was double-encoded as "%2525" in the query string).
        // After GetQueryParameter unescapes once, the segment is "Weird%25Folder"; the parser must NOT decode
        // again, or the folder name would collapse to "Weird%Folder".
        var (_, sitePath, libraryName, folderPath) = ResolveSharePointTargetActivity.ParseSharePointUrl(
            "https://tenant.sharepoint.com/sites/SiteName/LibraryName/Forms/AllItems.aspx" +
            "?id=%2Fsites%2FSiteName%2FLibraryName%2FWeird%2525Folder%2FSub");

        Assert.Equal("/sites/SiteName", sitePath);
        Assert.Equal("LibraryName", libraryName);
        Assert.Equal("Weird%25Folder/Sub", folderPath);
    }

    [Fact]
    public void ParseSharePointUrl_CopyLinkShareUrl_ThrowsArgumentExceptionWithHelpfulMessage()
    {
        var url = "https://tenant.sharepoint.com/:f:/s/SiteName/Ev1abc123xyz";

        var ex = Assert.Throws<ArgumentException>(
            () => ResolveSharePointTargetActivity.ParseSharePointUrl(url));

        Assert.Contains("Copy link", ex.Message);
        Assert.Contains("address bar", ex.Message);
        Assert.Contains(url, ex.Message);
    }

    [Theory]
    [InlineData("https://tenant.sharepoint.com/:w:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:x:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:b:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:p:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:o:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:v:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:i:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:t:/s/SiteName/Ev1abc")]
    [InlineData("https://tenant.sharepoint.com/:u:/s/SiteName/Ev1abc")]
    public void ParseSharePointUrl_CopyLinkShareUrl_OtherFileTypeVariants_AlsoRejected(string url)
    {
        Assert.Throws<ArgumentException>(() => ResolveSharePointTargetActivity.ParseSharePointUrl(url));
    }

    [Fact]
    public void SelectDrive_MatchesOnWebUrlSuffix_NotDisplayName()
    {
        // "Foo-"'s URL-safe slug drops the trailing dash, landing on "Foo" — which happens to be another
        // library's literal display Name. WebUrl matching must resolve to "Foo-" (the one whose real URL
        // this is), not "Foo" (whose Name merely coincides with the URL segment).
        var drives = new List<Drive>
        {
            new() { Name = "Foo", WebUrl = "https://tenant.sharepoint.com/sites/SiteName/Foo1" },
            new() { Name = "Foo-", WebUrl = "https://tenant.sharepoint.com/sites/SiteName/Foo" },
        };

        var result = ResolveSharePointTargetActivity.SelectDrive(drives, "Foo", "SiteName");

        Assert.Equal("Foo-", result.Name);
    }

    [Fact]
    public void SelectDrive_UrlSegmentWithAppendedSuffix_MatchesCorrectDrive()
    {
        var drives = new List<Drive>
        {
            new() { Name = "Foo", WebUrl = "https://tenant.sharepoint.com/sites/SiteName/Foo1" },
            new() { Name = "Foo-", WebUrl = "https://tenant.sharepoint.com/sites/SiteName/Foo" },
        };

        var result = ResolveSharePointTargetActivity.SelectDrive(drives, "Foo1", "SiteName");

        Assert.Equal("Foo", result.Name);
    }

    [Fact]
    public void SelectDrive_NoWebUrlMatch_ThrowsWithAvailableLibraryNames()
    {
        var drives = new List<Drive>
        {
            new() { Name = "Documents", WebUrl = "https://tenant.sharepoint.com/sites/SiteName/Shared Documents" },
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ResolveSharePointTargetActivity.SelectDrive(drives, "NotThere", "SiteName"));

        Assert.Contains("NotThere", ex.Message);
        Assert.Contains("SiteName", ex.Message);
        Assert.Contains("Documents", ex.Message);
    }
}
