namespace Bookdex.Core.Models;

public sealed class BookReadResult
{
    public BookReadResult(
        string sourcePath,
        string format,
        BookMetadata metadata,
        CoverImage? cover,
        IReadOnlyList<TextChunk> textChunks,
        IReadOnlyList<string> warnings)
    {
        SourcePath = sourcePath;
        Format = format;
        Metadata = metadata;
        Cover = cover;
        TextChunks = textChunks;
        Warnings = warnings;
    }

    public string SourcePath { get; }
    public string Format { get; }
    public BookMetadata Metadata { get; }
    public CoverImage? Cover { get; }
    public IReadOnlyList<TextChunk> TextChunks { get; }
    public IReadOnlyList<string> Warnings { get; }

    public static BookReadResult BestEffort(
        string sourcePath,
        string format,
        IEnumerable<string> warnings,
        BookMetadata? metadata = null)
    {
        return new BookReadResult(
            sourcePath,
            format,
            metadata ?? BookMetadata.CreateFallback(sourcePath),
            cover: null,
            textChunks: [],
            warnings: warnings.ToList());
    }
}
