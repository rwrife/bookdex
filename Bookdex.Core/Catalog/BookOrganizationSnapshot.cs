namespace Bookdex.Core.Catalog;

public sealed record BookOrganizationSnapshot(
    long BookId,
    BookReadingStatus? ReadingStatus,
    int? Rating,
    IReadOnlyList<string> Shelves,
    IReadOnlyList<string> Tags);
