namespace Bookdex.Core.Catalog;

public sealed record CatalogSearchResult(
    long BookId,
    string Title,
    string Authors,
    string Snippet,
    double Rank);
