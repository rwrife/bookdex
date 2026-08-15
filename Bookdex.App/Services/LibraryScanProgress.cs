namespace Bookdex.App.Services;

public sealed record LibraryScanProgress(
    int ProcessedFiles,
    int TotalFiles,
    int IndexedFiles,
    int SkippedFiles,
    int FailedFiles,
    string? CurrentFilePath);
