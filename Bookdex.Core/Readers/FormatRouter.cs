using Bookdex.Core.Models;
using Bookdex.Core.Readers.Cbz;
using Bookdex.Core.Readers.Epub;
using Bookdex.Core.Readers.Pdf;

namespace Bookdex.Core.Readers;

public sealed class FormatRouter
{
    private readonly IReadOnlyList<IBookFormatReader> _readers;

    public FormatRouter(IEnumerable<IBookFormatReader> readers)
    {
        _readers = readers.ToList();
    }

    public static FormatRouter CreateDefault()
    {
        return new FormatRouter([
            new EpubFormatReader(),
            new PdfFormatReader(),
            new CbzFormatReader(),
        ]);
    }

    public BookReadResult Read(string path)
    {
        if (!File.Exists(path))
        {
            return BookReadResult.BestEffort(path, "missing", ["File does not exist."]);
        }

        var header = ReadHeader(path, 16);

        foreach (var reader in _readers)
        {
            var canRead = false;
            try
            {
                canRead = reader.CanRead(path, header);
            }
            catch (Exception ex)
            {
                return BookReadResult.BestEffort(path, "unknown", [$"Reader probe failed: {ex.Message}"]);
            }

            if (!canRead)
            {
                continue;
            }

            try
            {
                return reader.Read(path);
            }
            catch (Exception ex)
            {
                return BookReadResult.BestEffort(path, reader.Format, [$"{reader.Format} reader failed: {ex.Message}"]);
            }
        }

        return BookReadResult.BestEffort(path, "unknown", ["No reader matched file type."]);
    }

    private static byte[] ReadHeader(string path, int byteCount)
    {
        var buffer = new byte[byteCount];
        using var stream = File.OpenRead(path);
        var read = stream.Read(buffer, 0, buffer.Length);
        return read == buffer.Length ? buffer : buffer[..read];
    }
}
