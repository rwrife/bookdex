namespace Bookdex.Core.Catalog;

public interface IReadingProgressStore
{
    void UpsertProgress(long bookId, string locator, double? progressPercent = null);

    ReadingProgressSnapshot? GetProgress(long bookId);

    IReadOnlyList<ReadingBookmark> GetBookmarks(long bookId);

    long AddBookmark(long bookId, string name, string locator);

    bool RemoveBookmark(long bookId, long bookmarkId);
}
