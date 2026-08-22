namespace Bookdex.Core.Storage;

public enum BookdexHostPlatform
{
    Windows,
    MacOS,
    Other,
}

public static class BookdexDataDirectory
{
    public const string DirectoryName = "bookdex";

    public static string GetDefaultPath()
    {
        var platform = OperatingSystem.IsWindows()
            ? BookdexHostPlatform.Windows
            : OperatingSystem.IsMacOS()
                ? BookdexHostPlatform.MacOS
                : BookdexHostPlatform.Other;

        return GetPath(
            platform,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    public static string GetPath(
        BookdexHostPlatform platform,
        string applicationDataPath,
        string userProfilePath,
        string localApplicationDataPath)
    {
        var path = platform switch
        {
            BookdexHostPlatform.Windows => Path.Combine(
                RequirePath(applicationDataPath, nameof(applicationDataPath)),
                DirectoryName),
            BookdexHostPlatform.MacOS => Path.Combine(
                RequirePath(userProfilePath, nameof(userProfilePath)),
                "Library",
                "Application Support",
                DirectoryName),
            _ => Path.Combine(
                RequirePath(localApplicationDataPath, nameof(localApplicationDataPath)),
                DirectoryName),
        };

        return Path.GetFullPath(path);
    }

    public static string CreateDefault()
    {
        var path = GetDefaultPath();
        Directory.CreateDirectory(path);
        return path;
    }

    public static string EnsureCreated(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Data directory path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static string RequirePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Platform base directory is required.", parameterName);
        }

        return path;
    }
}
