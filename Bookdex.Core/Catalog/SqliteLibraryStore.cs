using Bookdex.Core.Models;
using Microsoft.Data.Sqlite;

namespace Bookdex.Core.Catalog;

public sealed class SqliteLibraryStore : ILibraryStore
{
    private readonly string _databasePath;

    public SqliteLibraryStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS books (
                book_id INTEGER PRIMARY KEY AUTOINCREMENT,
                content_hash TEXT NOT NULL UNIQUE,
                title TEXT,
                series TEXT,
                series_index REAL,
                isbn TEXT,
                publisher TEXT,
                language TEXT,
                published_date TEXT,
                description TEXT,
                format TEXT NOT NULL,
                primary_path TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS authors (
                author_id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE
            );

            CREATE TABLE IF NOT EXISTS book_authors (
                book_id INTEGER NOT NULL,
                author_id INTEGER NOT NULL,
                PRIMARY KEY (book_id, author_id),
                FOREIGN KEY(book_id) REFERENCES books(book_id) ON DELETE CASCADE,
                FOREIGN KEY(author_id) REFERENCES authors(author_id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS file_signatures (
                path TEXT PRIMARY KEY,
                size_bytes INTEGER NOT NULL,
                mtime_utc_ticks INTEGER NOT NULL,
                content_hash TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_file_signatures_content_hash
                ON file_signatures(content_hash);

            CREATE VIRTUAL TABLE IF NOT EXISTS book_fts
                USING fts5(book_id UNINDEXED, title, authors, content);
            """;
        command.ExecuteNonQuery();
    }

    public bool ShouldScan(FileSignature signature)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT size_bytes, mtime_utc_ticks
            FROM file_signatures
            WHERE path = $path;
            """;
        command.Parameters.AddWithValue("$path", NormalizePath(signature.Path));

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return true;
        }

        var size = reader.GetInt64(0);
        var mtime = reader.GetInt64(1);
        return size != signature.SizeBytes || mtime != signature.LastWriteTimeUtcTicks;
    }

    public long UpsertBook(CatalogBookRecord record, FileSignature signature)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(signature);

        var now = DateTimeOffset.UtcNow;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var title = record.Metadata.Title?.Trim();
        var normalizedPath = NormalizePath(record.SourcePath);

        var bookId = FindBookIdByContentHash(connection, transaction, record.ContentHash);
        if (bookId is null)
        {
            using var insertBook = connection.CreateCommand();
            insertBook.Transaction = transaction;
            insertBook.CommandText = """
                INSERT INTO books (
                    content_hash,
                    title,
                    series,
                    series_index,
                    isbn,
                    publisher,
                    language,
                    published_date,
                    description,
                    format,
                    primary_path,
                    created_utc,
                    updated_utc)
                VALUES (
                    $content_hash,
                    $title,
                    $series,
                    $series_index,
                    $isbn,
                    $publisher,
                    $language,
                    $published_date,
                    $description,
                    $format,
                    $primary_path,
                    $created_utc,
                    $updated_utc);

                SELECT last_insert_rowid();
                """;
            BindBookMetadata(insertBook, record, normalizedPath, now);
            bookId = Convert.ToInt64(insertBook.ExecuteScalar());
        }
        else
        {
            using var updateBook = connection.CreateCommand();
            updateBook.Transaction = transaction;
            updateBook.CommandText = """
                UPDATE books
                SET
                    title = $title,
                    series = $series,
                    series_index = $series_index,
                    isbn = $isbn,
                    publisher = $publisher,
                    language = $language,
                    published_date = $published_date,
                    description = $description,
                    format = $format,
                    primary_path = $primary_path,
                    updated_utc = $updated_utc
                WHERE book_id = $book_id;
                """;
            BindBookMetadata(updateBook, record, normalizedPath, now, includeCreatedUtc: false);
            updateBook.Parameters.AddWithValue("$book_id", bookId.Value);
            updateBook.ExecuteNonQuery();
        }

        var normalizedAuthors = record.Metadata.Authors
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        using (var clearAuthorLinks = connection.CreateCommand())
        {
            clearAuthorLinks.Transaction = transaction;
            clearAuthorLinks.CommandText = "DELETE FROM book_authors WHERE book_id = $book_id;";
            clearAuthorLinks.Parameters.AddWithValue("$book_id", bookId.Value);
            clearAuthorLinks.ExecuteNonQuery();
        }

        foreach (var author in normalizedAuthors)
        {
            var authorId = EnsureAuthor(connection, transaction, author);
            using var addBookAuthor = connection.CreateCommand();
            addBookAuthor.Transaction = transaction;
            addBookAuthor.CommandText = """
                INSERT OR IGNORE INTO book_authors (book_id, author_id)
                VALUES ($book_id, $author_id);
                """;
            addBookAuthor.Parameters.AddWithValue("$book_id", bookId.Value);
            addBookAuthor.Parameters.AddWithValue("$author_id", authorId);
            addBookAuthor.ExecuteNonQuery();
        }

        UpsertFts(connection, transaction, bookId.Value, title ?? string.Empty, string.Join(", ", normalizedAuthors), record.FullTextContent);
        UpsertSignature(connection, transaction, signature, record.ContentHash, now);

        transaction.Commit();
        return bookId.Value;
    }

    public IReadOnlyList<CatalogSearchResult> Search(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return [];
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                b.book_id,
                COALESCE(b.title, ''),
                COALESCE(author_rollup.authors, ''),
                COALESCE(snippet(book_fts, 3, '[', ']', '…', 12), ''),
                bm25(book_fts)
            FROM book_fts
            INNER JOIN books b ON b.book_id = CAST(book_fts.book_id AS INTEGER)
            LEFT JOIN (
                SELECT
                    ba.book_id,
                    group_concat(a.name, ', ') AS authors
                FROM book_authors ba
                INNER JOIN authors a ON a.author_id = ba.author_id
                GROUP BY ba.book_id
            ) AS author_rollup ON author_rollup.book_id = b.book_id
            WHERE book_fts MATCH $query
            ORDER BY bm25(book_fts), b.book_id ASC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$query", query.Trim());
        command.Parameters.AddWithValue("$limit", limit);

        using var reader = command.ExecuteReader();
        var results = new List<CatalogSearchResult>();
        while (reader.Read())
        {
            results.Add(new CatalogSearchResult(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? double.MaxValue : reader.GetDouble(4)));
        }

        return results;
    }

    public int GetBookCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM books;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int GetTrackedFileCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM file_signatures;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static long? FindBookIdByContentHash(SqliteConnection connection, SqliteTransaction transaction, string contentHash)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT book_id FROM books WHERE content_hash = $content_hash LIMIT 1;";
        command.Parameters.AddWithValue("$content_hash", contentHash);

        var scalar = command.ExecuteScalar();
        return scalar is null ? null : Convert.ToInt64(scalar);
    }

    private static void BindBookMetadata(
        SqliteCommand command,
        CatalogBookRecord record,
        string normalizedPath,
        DateTimeOffset timestamp,
        bool includeCreatedUtc = true)
    {
        command.Parameters.AddWithValue("$content_hash", record.ContentHash);
        command.Parameters.AddWithValue("$title", (object?)record.Metadata.Title?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$series", (object?)record.Metadata.Series?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$series_index", (object?)record.Metadata.SeriesIndex ?? DBNull.Value);
        command.Parameters.AddWithValue("$isbn", (object?)record.Metadata.Isbn?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$publisher", (object?)record.Metadata.Publisher?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$language", (object?)record.Metadata.Language?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$published_date", (object?)record.Metadata.PublishedDate?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)record.Metadata.Description?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue("$format", record.Format);
        command.Parameters.AddWithValue("$primary_path", normalizedPath);

        if (includeCreatedUtc)
        {
            command.Parameters.AddWithValue("$created_utc", timestamp.ToString("O"));
        }

        command.Parameters.AddWithValue("$updated_utc", timestamp.ToString("O"));
    }

    private static long EnsureAuthor(SqliteConnection connection, SqliteTransaction transaction, string authorName)
    {
        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO authors (name) VALUES ($name);";
            insert.Parameters.AddWithValue("$name", authorName);
            insert.ExecuteNonQuery();
        }

        using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = "SELECT author_id FROM authors WHERE name = $name LIMIT 1;";
            lookup.Parameters.AddWithValue("$name", authorName);
            return Convert.ToInt64(lookup.ExecuteScalar());
        }
    }

    private static void UpsertFts(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long bookId,
        string title,
        string authors,
        string fullTextContent)
    {
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM book_fts WHERE book_id = $book_id;";
            delete.Parameters.AddWithValue("$book_id", bookId.ToString());
            delete.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO book_fts (book_id, title, authors, content)
                VALUES ($book_id, $title, $authors, $content);
                """;
            insert.Parameters.AddWithValue("$book_id", bookId.ToString());
            insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$authors", authors);
            insert.Parameters.AddWithValue("$content", fullTextContent ?? string.Empty);
            insert.ExecuteNonQuery();
        }
    }

    private static void UpsertSignature(
        SqliteConnection connection,
        SqliteTransaction transaction,
        FileSignature signature,
        string contentHash,
        DateTimeOffset timestamp)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO file_signatures (path, size_bytes, mtime_utc_ticks, content_hash, updated_utc)
            VALUES ($path, $size_bytes, $mtime_utc_ticks, $content_hash, $updated_utc)
            ON CONFLICT(path) DO UPDATE SET
                size_bytes = excluded.size_bytes,
                mtime_utc_ticks = excluded.mtime_utc_ticks,
                content_hash = excluded.content_hash,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.AddWithValue("$path", NormalizePath(signature.Path));
        command.Parameters.AddWithValue("$size_bytes", signature.SizeBytes);
        command.Parameters.AddWithValue("$mtime_utc_ticks", signature.LastWriteTimeUtcTicks);
        command.Parameters.AddWithValue("$content_hash", contentHash);
        command.Parameters.AddWithValue("$updated_utc", timestamp.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path);
    }
}
