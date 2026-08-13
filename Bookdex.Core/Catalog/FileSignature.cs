namespace Bookdex.Core.Catalog;

public sealed record FileSignature(
    string Path,
    long SizeBytes,
    long LastWriteTimeUtcTicks)
{
    public static FileSignature FromFile(string path)
    {
        var info = new FileInfo(path);
        return new FileSignature(
            path,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc.Ticks : 0);
    }
}
