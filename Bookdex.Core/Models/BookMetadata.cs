namespace Bookdex.Core.Models;

public sealed class BookMetadata
{
    public string? Title { get; set; }
    public List<string> Authors { get; } = [];
    public string? Series { get; set; }
    public double? SeriesIndex { get; set; }
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public string? Language { get; set; }
    public DateOnly? PublishedDate { get; set; }
    public List<string> Subjects { get; } = [];
    public string? Description { get; set; }

    public static BookMetadata CreateFallback(string sourcePath)
    {
        return new BookMetadata
        {
            Title = Path.GetFileNameWithoutExtension(sourcePath),
        };
    }
}
