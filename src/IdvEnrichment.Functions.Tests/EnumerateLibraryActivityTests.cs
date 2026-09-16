using IdvEnrichment.Functions.Activities;
using Xunit;

namespace IdvEnrichment.Functions.Tests;

public class EnumerateLibraryActivityTests
{
    [Theory]
    [InlineData("~$Report.xlsx")]
    [InlineData("~$Notes.docx")]
    public void IsOfficeLockFile_LockFilePrefix_ReturnsTrue(string name)
    {
        Assert.True(EnumerateLibraryActivity.IsOfficeLockFile(name));
    }

    [Theory]
    [InlineData("Report.xlsx")]
    [InlineData("Notes~$1.docx")]
    [InlineData("")]
    public void IsOfficeLockFile_NotLockFile_ReturnsFalse(string name)
    {
        Assert.False(EnumerateLibraryActivity.IsOfficeLockFile(name));
    }

    [Fact]
    public void IsOfficeLockFile_NullName_ReturnsFalse()
    {
        Assert.False(EnumerateLibraryActivity.IsOfficeLockFile(null));
    }
}
