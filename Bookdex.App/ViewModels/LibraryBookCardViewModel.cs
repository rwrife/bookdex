using Bookdex.Core.Catalog;

namespace Bookdex.App.ViewModels;

public sealed class LibraryBookCardViewModel
{
    public required long BookId { get; init; }

    public required string Title { get; init; }

    public required string Authors { get; init; }

    public required string Snippet { get; init; }

    public string PrimaryPath { get; init; } = string.Empty;

    public static LibraryBookCardViewModel FromSearchResult(CatalogSearchResult result)
    {
        return new LibraryBookCardViewModel
        {
            BookId = result.BookId,
            Title = string.IsNullOrWhiteSpace(result.Title) ? "(untitled)" : result.Title,
            Authors = string.IsNullOrWhiteSpace(result.Authors) ? "Unknown author" : result.Authors,
            Snippet = string.IsNullOrWhiteSpace(result.Snippet) ? "No preview text yet." : result.Snippet,
            PrimaryPath = string.IsNullOrWhiteSpace(result.PrimaryPath) ? "(path unavailable)" : result.PrimaryPath,
        };
    }
}
