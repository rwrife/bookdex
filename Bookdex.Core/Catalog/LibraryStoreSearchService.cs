namespace Bookdex.Core.Catalog;

public sealed class LibraryStoreSearchService : ISearchService
{
    private readonly ILibraryStore _store;

    public LibraryStoreSearchService(ILibraryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return [];
        }

        return _store.Search(query.Trim(), limit);
    }
}
