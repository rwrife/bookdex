using Bookdex.Core.Catalog;

namespace Bookdex.Core.Ai;

public sealed record LocalAiSearchOutcome(
    IReadOnlyList<CatalogSearchResult> Results,
    LocalAiSearchMode Mode,
    string? Message);
