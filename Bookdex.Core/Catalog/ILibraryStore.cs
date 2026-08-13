namespace Bookdex.Core.Catalog;

public interface ILibraryStore
{
    void Initialize();

    bool ShouldScan(FileSignature signature);

    long UpsertBook(CatalogBookRecord record, FileSignature signature);

    IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 20);

    void ReplaceBookShelves(long bookId, IEnumerable<string> shelfNames);

    void ReplaceBookTags(long bookId, IEnumerable<string> tagNames);

    void SetReadingState(long bookId, BookReadingStatus? status, int? rating);

    BookOrganizationSnapshot GetBookOrganization(long bookId);
}
