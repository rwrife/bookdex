namespace Bookdex.App.Services;

public sealed record LibraryScanResult(
    string FolderPath,
    int TotalFiles,
    int IndexedFiles,
    int SkippedFiles,
    int FailedFiles);
