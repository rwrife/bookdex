namespace Bookdex.Core.Catalog;

public interface ISearchService
{
    IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 50);
}
