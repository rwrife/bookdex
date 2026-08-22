namespace Bookdex.Core.Catalog;

public sealed record ReadingBookmark(
    long BookmarkId,
    long BookId,
    string Name,
    string Locator,
    DateTimeOffset CreatedUtc);
