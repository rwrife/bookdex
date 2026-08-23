using Bookdex.Core.Storage;

namespace Bookdex.Core.Tests;

public sealed class BookdexDataDirectoryTests
{
    [Fact]
    public void GetPath_UsesRoamingApplicationDataOnWindows()
    {
        var applicationData = Path.Combine(Path.GetTempPath(), "profile", "AppData", "Roaming");

        var path = BookdexDataDirectory.GetPath(
            BookdexHostPlatform.Windows,
            applicationData,
            Path.Combine(Path.GetTempPath(), "profile"),
            Path.Combine(Path.GetTempPath(), "profile", "AppData", "Local"));

        Assert.Equal(Path.GetFullPath(Path.Combine(applicationData, "bookdex")), path);
    }

    [Fact]
    public void GetPath_UsesApplicationSupportOnMacOS()
    {
        var userProfile = Path.Combine(Path.GetTempPath(), "Users", "reader");

        var path = BookdexDataDirectory.GetPath(
            BookdexHostPlatform.MacOS,
            Path.Combine(userProfile, "Library", "Application Data"),
            userProfile,
            Path.Combine(userProfile, ".local", "share"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(userProfile, "Library", "Application Support", "bookdex")),
            path);
    }

    [Fact]
    public void GetPath_UsesLocalApplicationDataOnOtherPlatforms()
    {
        var localApplicationData = Path.Combine(Path.GetTempPath(), "profile", ".local", "share");

        var path = BookdexDataDirectory.GetPath(
            BookdexHostPlatform.Other,
            Path.Combine(Path.GetTempPath(), "profile", ".config"),
            Path.Combine(Path.GetTempPath(), "profile"),
            localApplicationData);

        Assert.Equal(Path.GetFullPath(Path.Combine(localApplicationData, "bookdex")), path);
    }

    [Fact]
    public void EnsureCreated_CreatesTheFirstRunDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "bookdex-tests",
            "data-directory",
            Guid.NewGuid().ToString("N"));

        try
        {
            var path = BookdexDataDirectory.EnsureCreated(Path.Combine(root, "nested", "bookdex"));

            Assert.True(Directory.Exists(path));
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "nested", "bookdex")), path);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPath_RejectsMissingSelectedPlatformBaseDirectory()
    {
        var exception = Assert.Throws<ArgumentException>(() => BookdexDataDirectory.GetPath(
            BookdexHostPlatform.Windows,
            string.Empty,
            "/unused/profile",
            "/unused/local"));

        Assert.Equal("applicationDataPath", exception.ParamName);
    }
}
