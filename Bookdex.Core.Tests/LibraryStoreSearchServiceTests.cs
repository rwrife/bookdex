using Bookdex.Core.Catalog;

namespace Bookdex.Core.Tests;

public sealed class LibraryStoreSearchServiceTests
{
    [Fact]
    public void Search_WhenQueryIsBlank_ReturnsEmptyWithoutCallingStore()
    {
        var store = new SpyLibraryStore();
        var service = new LibraryStoreSearchService(store);

        var results = service.Search("   ");

        Assert.Empty(results);
        Assert.Equal(0, store.SearchCallCount);
    }

    [Fact]
    public void Search_WhenQueryHasValue_TrimsAndDelegatesToStore()
    {
        var expected = new[]
        {
            new CatalogSearchResult(42, "A Fire Upon the Deep", "Vernor Vinge", "space opera", 0.02, "/books/fire.epub"),
        };

        var store = new SpyLibraryStore(expected);
        var service = new LibraryStoreSearchService(store);

        var results = service.Search("  vinge  ", limit: 13);

        Assert.Equal(expected, results);
        Assert.Equal(1, store.SearchCallCount);
        Assert.Equal("vinge", store.LastQuery);
        Assert.Equal(13, store.LastLimit);
    }

    private sealed class SpyLibraryStore(IReadOnlyList<CatalogSearchResult>? results = null) : ILibraryStore
    {
        public int SearchCallCount { get; private set; }

        public string LastQuery { get; private set; } = string.Empty;

        public int LastLimit { get; private set; }

        public void Initialize() => throw new NotSupportedException();

        public bool ShouldScan(FileSignature signature) => throw new NotSupportedException();

        public long UpsertBook(CatalogBookRecord record, FileSignature signature) => throw new NotSupportedException();

        public IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 20)
        {
            SearchCallCount++;
            LastQuery = query;
            LastLimit = limit;
            return results ?? [];
        }

        public void ReplaceBookShelves(long bookId, IEnumerable<string> shelfNames) => throw new NotSupportedException();

        public void ReplaceBookTags(long bookId, IEnumerable<string> tagNames) => throw new NotSupportedException();

        public void SetReadingState(long bookId, BookReadingStatus? status, int? rating) => throw new NotSupportedException();

        public BookOrganizationSnapshot GetBookOrganization(long bookId) => throw new NotSupportedException();
    }
}
