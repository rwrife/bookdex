using Bookdex.Core.Models;

namespace Bookdex.Core.Readers;

public interface IBookFormatReader
{
    string Format { get; }

    bool CanRead(string path, ReadOnlySpan<byte> header);

    BookReadResult Read(string path);
}
