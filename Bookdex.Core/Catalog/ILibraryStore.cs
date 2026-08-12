namespace Bookdex.Core.Catalog;

public interface ILibraryStore
{
    void Initialize();

    bool ShouldScan(FileSignature signature);

    long UpsertBook(CatalogBookRecord record, FileSignature signature);

    IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 20);
}
