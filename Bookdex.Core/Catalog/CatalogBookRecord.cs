using Bookdex.Core.Models;

namespace Bookdex.Core.Catalog;

public sealed record CatalogBookRecord(
    string SourcePath,
    string Format,
    string ContentHash,
    BookMetadata Metadata,
    string FullTextContent);
