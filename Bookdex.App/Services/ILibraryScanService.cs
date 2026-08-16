namespace Bookdex.App.Services;

public interface ILibraryScanService
{
    Task<LibraryScanResult> ScanFolderAsync(
        string folderPath,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
