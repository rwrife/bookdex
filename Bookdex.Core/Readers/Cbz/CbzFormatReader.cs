using System.IO.Compression;
using System.Xml.Linq;
using Bookdex.Core.Models;
using Bookdex.Core.Readers.Internal;

namespace Bookdex.Core.Readers.Cbz;

public sealed class CbzFormatReader : IBookFormatReader
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    ];

    public string Format => "cbz";

    public bool CanRead(string path, ReadOnlySpan<byte> header)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return header.Length >= 2 && header[0] == 'P' && header[1] == 'K';
    }

    public BookReadResult Read(string path)
    {
        var warnings = new List<string>();
        var metadata = BookMetadata.CreateFallback(path);
        CoverImage? cover = null;

        if (Path.GetExtension(path).Equals(".cbr", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("CBR/RAR parsing is not implemented in this slice; provide CBZ for full extraction.");
            return new BookReadResult(path, Format, metadata, cover, [], warnings);
        }

        using var archive = ZipFile.OpenRead(path);

        var comicInfo = archive.Entries.FirstOrDefault(x =>
            string.Equals(x.FullName, "ComicInfo.xml", StringComparison.OrdinalIgnoreCase));

        if (comicInfo is not null)
        {
            try
            {
                using var stream = comicInfo.Open();
                var doc = XDocument.Load(stream);

                metadata.Title = FirstNonEmpty(GetNodeValue(doc, "Title"), metadata.Title);
                AddUnique(metadata.Authors, GetNodeValue(doc, "Writer"));
                metadata.Series = FirstNonEmpty(GetNodeValue(doc, "Series"), metadata.Series);
                metadata.Publisher = FirstNonEmpty(GetNodeValue(doc, "Publisher"), metadata.Publisher);
                metadata.Language = FirstNonEmpty(GetNodeValue(doc, "LanguageISO"), metadata.Language);
                metadata.Description = FirstNonEmpty(GetNodeValue(doc, "Summary"), metadata.Description);

                var genreRaw = GetNodeValue(doc, "Genre");
                if (!string.IsNullOrWhiteSpace(genreRaw))
                {
                    foreach (var part in genreRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        AddUnique(metadata.Subjects, part);
                    }
                }

                var numberRaw = GetNodeValue(doc, "Number");
                if (!string.IsNullOrWhiteSpace(numberRaw)
                    && double.TryParse(numberRaw, out var seriesIndex))
                {
                    metadata.SeriesIndex = seriesIndex;
                }

                var yearRaw = GetNodeValue(doc, "Year");
                var monthRaw = GetNodeValue(doc, "Month");
                var dayRaw = GetNodeValue(doc, "Day");
                if (int.TryParse(yearRaw, out var year))
                {
                    var month = int.TryParse(monthRaw, out var parsedMonth) ? Math.Clamp(parsedMonth, 1, 12) : 1;
                    var day = int.TryParse(dayRaw, out var parsedDay)
                        ? Math.Clamp(parsedDay, 1, DateTime.DaysInMonth(year, month))
                        : 1;

                    metadata.PublishedDate = new DateOnly(year, month, day);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse ComicInfo.xml: {ex.Message}");
            }
        }
        else
        {
            warnings.Add("ComicInfo.xml not present; using fallback metadata.");
        }

        var coverEntry = archive.Entries
            .Where(e => ImageExtensions.Contains(Path.GetExtension(e.FullName).ToLowerInvariant()))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (coverEntry is not null)
        {
            try
            {
                using var stream = coverEntry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                cover = new CoverImage(GetMimeType(coverEntry.FullName), memory.ToArray(), coverEntry.FullName);
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to extract cover image: {ex.Message}");
            }
        }
        else
        {
            warnings.Add("No image entries found for cover extraction.");
        }

        var summaryText = metadata.Description ?? string.Empty;
        var textChunks = TextChunker.Chunk(summaryText);

        return new BookReadResult(path, Format, metadata, cover, textChunks, warnings);
    }

    private static string? GetNodeValue(XDocument doc, string nodeName)
    {
        return doc.Descendants().FirstOrDefault(x => x.Name.LocalName == nodeName)?.Value?.Trim();
    }

    private static string? FirstNonEmpty(string? value, string? fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static void AddUnique(ICollection<string> destination, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (destination.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        destination.Add(trimmed);
    }

    private static string GetMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };
    }
}
