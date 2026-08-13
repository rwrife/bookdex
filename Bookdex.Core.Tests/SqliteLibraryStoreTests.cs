using System.Security.Cryptography;
using System.Text;
using Bookdex.Core.Catalog;
using Bookdex.Core.Models;

namespace Bookdex.Core.Tests;

public sealed class SqliteLibraryStoreTests
{
    [Fact]
    public void ShouldScan_UsesStoredPathSizeAndMtimeSignature()
    {
        using var fixture = CreateFixture();
        var sourcePath = Path.Combine(fixture.RootDir, "book-a.epub");
        var baselineSignature = new FileSignature(sourcePath, SizeBytes: 1024, LastWriteTimeUtcTicks: 100);

        Assert.True(fixture.Store.ShouldScan(baselineSignature));

        var firstBook = CreateBookRecord(
            sourcePath,
            title: "Scanner Test Book",
            contentHash: Hash("scanner test content"),
            fullText: "scanner test content",
            authors: ["Ada"]);

        fixture.Store.UpsertBook(firstBook, baselineSignature);

        Assert.False(fixture.Store.ShouldScan(baselineSignature));
        Assert.True(fixture.Store.ShouldScan(baselineSignature with { LastWriteTimeUtcTicks = 101 }));
        Assert.True(fixture.Store.ShouldScan(baselineSignature with { SizeBytes = 2048 }));
    }

    [Fact]
    public void UpsertBook_DedupesByContentHashAndTracksBothPaths()
    {
        using var fixture = CreateFixture();

        var duplicateHash = Hash("same binary content");
        var firstPath = Path.Combine(fixture.RootDir, "a.epub");
        var secondPath = Path.Combine(fixture.RootDir, "nested", "b.epub");

        var first = CreateBookRecord(
            firstPath,
            title: "First Title",
            contentHash: duplicateHash,
            fullText: "Library copy one",
            authors: ["Author One"]);

        var second = CreateBookRecord(
            secondPath,
            title: "Second Title Should Merge",
            contentHash: duplicateHash,
            fullText: "Library copy two",
            authors: ["Author One"]);

        var firstId = fixture.Store.UpsertBook(first, new FileSignature(firstPath, 123, 1));
        var secondId = fixture.Store.UpsertBook(second, new FileSignature(secondPath, 123, 1));

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, fixture.Store.GetBookCount());
        Assert.Equal(2, fixture.Store.GetTrackedFileCount());
    }

    [Fact]
    public void Search_ReturnsRankedFtsMatchesFromTitleAuthorsAndContent()
    {
        using var fixture = CreateFixture();

        fixture.Store.UpsertBook(
            CreateBookRecord(
                Path.Combine(fixture.RootDir, "ship.epub"),
                title: "Generation Ship Librarian",
                contentHash: Hash("book-one"),
                fullText: "A librarian archives stories on a generation ship. Librarian records every voyage.",
                authors: ["Nora Kepler"]),
            new FileSignature(Path.Combine(fixture.RootDir, "ship.epub"), 200, 10));

        fixture.Store.UpsertBook(
            CreateBookRecord(
                Path.Combine(fixture.RootDir, "cookbook.epub"),
                title: "Galley Cookbook",
                contentHash: Hash("book-two"),
                fullText: "Ship recipes and nutrition logs for long voyages.",
                authors: ["Chef Orion"]),
            new FileSignature(Path.Combine(fixture.RootDir, "cookbook.epub"), 180, 10));

        var results = fixture.Store.Search("librarian ship", limit: 5);

        Assert.NotEmpty(results);
        Assert.Equal("Generation Ship Librarian", results[0].Title);
        Assert.Contains("Nora", results[0].Authors);
    }

    [Fact]
    public void Organization_PersistsShelvesTagsStatusAndRating()
    {
        using var fixture = CreateFixture();

        var sourcePath = Path.Combine(fixture.RootDir, "organize.epub");
        var bookId = fixture.Store.UpsertBook(
            CreateBookRecord(
                sourcePath,
                title: "Organization Fixture",
                contentHash: Hash("organization-fixture"),
                fullText: "Organizing titles with tags and shelves.",
                authors: ["Alex Archivist"]),
            new FileSignature(sourcePath, 400, 50));

        fixture.Store.ReplaceBookShelves(bookId, [" Favorites ", "Sci-Fi", "favorites"]);
        fixture.Store.ReplaceBookTags(bookId, ["space opera", "Classic", "space opera"]);
        fixture.Store.SetReadingState(bookId, BookReadingStatus.Reading, 4);

        var first = fixture.Store.GetBookOrganization(bookId);

        Assert.Equal(BookReadingStatus.Reading, first.ReadingStatus);
        Assert.Equal(4, first.Rating);
        Assert.Equal(new[] { "Favorites", "Sci-Fi" }, first.Shelves);
        Assert.Equal(new[] { "Classic", "space opera" }, first.Tags);

        fixture.Store.ReplaceBookShelves(bookId, ["Archive"]);
        fixture.Store.ReplaceBookTags(bookId, ["history"]);
        fixture.Store.SetReadingState(bookId, null, null);

        var second = fixture.Store.GetBookOrganization(bookId);

        Assert.Null(second.ReadingStatus);
        Assert.Null(second.Rating);
        Assert.Equal(new[] { "Archive" }, second.Shelves);
        Assert.Equal(new[] { "history" }, second.Tags);
    }

    [Fact]
    public void SetReadingState_RejectsOutOfRangeRatings()
    {
        using var fixture = CreateFixture();

        var sourcePath = Path.Combine(fixture.RootDir, "ratings.epub");
        var bookId = fixture.Store.UpsertBook(
            CreateBookRecord(
                sourcePath,
                title: "Rating Fixture",
                contentHash: Hash("rating-fixture"),
                fullText: "Rating validation",
                authors: ["Robin Reader"]),
            new FileSignature(sourcePath, 320, 77));

        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Store.SetReadingState(bookId, BookReadingStatus.Unread, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Store.SetReadingState(bookId, BookReadingStatus.Unread, 6));
    }

    private static CatalogBookRecord CreateBookRecord(
        string sourcePath,
        string title,
        string contentHash,
        string fullText,
        IEnumerable<string> authors)
    {
        var metadata = new BookMetadata
        {
            Title = title,
            Description = "Fixture metadata",
            Language = "en",
            Publisher = "Bookdex Tests",
        };
        metadata.Authors.AddRange(authors);

        return new CatalogBookRecord(
            SourcePath: sourcePath,
            Format: "epub",
            ContentHash: contentHash,
            Metadata: metadata,
            FullTextContent: fullText);
    }

    private static TestFixture CreateFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "bookdex-tests", "sqlite-store", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var dbPath = Path.Combine(root, "catalog.db");
        var store = new SqliteLibraryStore(dbPath);
        store.Initialize();

        return new TestFixture(root, store);
    }

    private static string Hash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record TestFixture(string RootDir, SqliteLibraryStore Store) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootDir))
                {
                    Directory.Delete(RootDir, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup for temp directories.
            }
        }
    }
}
