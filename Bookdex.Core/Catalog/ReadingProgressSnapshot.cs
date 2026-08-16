namespace Bookdex.Core.Catalog;

public sealed record ReadingProgressSnapshot(
    long BookId,
    string Locator,
    double? ProgressPercent,
    DateTimeOffset UpdatedUtc);
