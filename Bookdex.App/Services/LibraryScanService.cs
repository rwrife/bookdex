using System.Security.Cryptography;
using Bookdex.Core.Catalog;
using Bookdex.Core.Readers;

namespace Bookdex.App.Services;

public sealed class LibraryScanService : ILibraryScanService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".epub",
        ".pdf",
        ".cbz",
    };

    private readonly ILibraryStore _store;
    private readonly FormatRouter _router;

    public LibraryScanService(ILibraryStore store, FormatRouter? router = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _router = router ?? FormatRouter.CreateDefault();
    }

    public async Task<LibraryScanResult> ScanFolderAsync(
        string folderPath,
        IProgress<LibraryScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Library folder does not exist: {fullPath}");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        var files = Directory.EnumerateFiles(fullPath, "*", options)
            .Where(IsSupportedFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = files.Count;
        var processed = 0;
        var indexed = 0;
        var skipped = 0;
        var failed = 0;

        progress?.Report(new LibraryScanProgress(processed, total, indexed, skipped, failed, null));

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            try
            {
                var fileInfo = new FileInfo(path);
                var signature = new FileSignature(path, fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);

                if (!_store.ShouldScan(signature))
                {
                    skipped++;
                    progress?.Report(new LibraryScanProgress(processed, total, indexed, skipped, failed, path));
                    continue;
                }

                var readResult = _router.Read(path);
                var contentHash = await ComputeSha256Async(path, cancellationToken);
                var fullText = string.Join(Environment.NewLine, readResult.TextChunks.Select(chunk => chunk.Text));

                var record = new CatalogBookRecord(
                    SourcePath: path,
                    Format: readResult.Format,
                    ContentHash: contentHash,
                    Metadata: readResult.Metadata,
                    FullTextContent: fullText);

                _store.UpsertBook(record, signature);
                indexed++;
            }
            catch
            {
                failed++;
            }

            progress?.Report(new LibraryScanProgress(processed, total, indexed, skipped, failed, path));
        }

        return new LibraryScanResult(fullPath, total, indexed, skipped, failed);
    }

    private static bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
