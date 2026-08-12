using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Bookdex.Core.Models;
using Bookdex.Core.Readers.Internal;

namespace Bookdex.Core.Readers.Epub;

public sealed class EpubFormatReader : IBookFormatReader
{
    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    ];

    public string Format => "epub";

    public bool CanRead(string path, ReadOnlySpan<byte> header)
    {
        _ = header;
        return Path.GetExtension(path).Equals(".epub", StringComparison.OrdinalIgnoreCase);
    }

    public BookReadResult Read(string path)
    {
        var warnings = new List<string>();
        var metadata = BookMetadata.CreateFallback(path);
        CoverImage? cover = null;
        IReadOnlyList<TextChunk> textChunks = [];

        using var archive = ZipFile.OpenRead(path);

        var opfPath = TryResolveOpfPath(archive, warnings);
        if (opfPath is null)
        {
            return new BookReadResult(path, Format, metadata, cover, textChunks, warnings);
        }

        var opfEntry = FindZipEntry(archive, opfPath);
        if (opfEntry is null)
        {
            warnings.Add($"OPF entry '{opfPath}' was not found.");
            return new BookReadResult(path, Format, metadata, cover, textChunks, warnings);
        }

        XDocument opfDoc;
        try
        {
            using var stream = opfEntry.Open();
            opfDoc = XDocument.Load(stream);
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to parse OPF: {ex.Message}");
            return new BookReadResult(path, Format, metadata, cover, textChunks, warnings);
        }

        var opfDirectory = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? string.Empty;

        ParseMetadata(opfDoc, metadata, warnings);

        var manifestById = BuildManifestMap(opfDoc);
        var coverPath = ResolveCoverPath(opfDoc, manifestById);

        if (!string.IsNullOrWhiteSpace(coverPath))
        {
            cover = TryReadCover(archive, ResolveZipPath(opfDirectory, coverPath), warnings);
        }

        cover ??= TryReadCoverFromFirstImage(archive, warnings);

        textChunks = ExtractSpineTextChunks(archive, opfDoc, manifestById, opfDirectory, warnings);

        if (textChunks.Count == 0)
        {
            warnings.Add("No readable spine text was extracted.");
        }

        return new BookReadResult(path, Format, metadata, cover, textChunks, warnings);
    }

    private static string? TryResolveOpfPath(ZipArchive archive, List<string> warnings)
    {
        var containerEntry = FindZipEntry(archive, "META-INF/container.xml");
        if (containerEntry is not null)
        {
            try
            {
                using var stream = containerEntry.Open();
                var doc = XDocument.Load(stream);
                var rootfile = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "rootfile");
                var path = rootfile?.Attribute("full-path")?.Value;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to parse container.xml: {ex.Message}");
            }
        }

        var fallback = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));

        if (fallback is null)
        {
            warnings.Add("No OPF package document found.");
            return null;
        }

        warnings.Add("container.xml missing or invalid; falling back to first .opf entry.");
        return fallback.FullName;
    }

    private static void ParseMetadata(XDocument opfDoc, BookMetadata metadata, List<string> warnings)
    {
        var metadataNode = opfDoc.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");
        if (metadataNode is null)
        {
            warnings.Add("OPF metadata element missing.");
            return;
        }

        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        metadata.Title = FirstNonEmpty(
            metadataNode.Elements(dc + "title").Select(x => x.Value),
            metadata.Title);

        foreach (var creator in metadataNode.Elements(dc + "creator").Select(x => x.Value))
        {
            AddUnique(metadata.Authors, creator);
        }

        metadata.Publisher = FirstNonEmpty(
            metadataNode.Elements(dc + "publisher").Select(x => x.Value),
            metadata.Publisher);

        metadata.Language = FirstNonEmpty(
            metadataNode.Elements(dc + "language").Select(x => x.Value),
            metadata.Language);

        metadata.Description = FirstNonEmpty(
            metadataNode.Elements(dc + "description").Select(x => x.Value),
            metadata.Description);

        var identifiers = metadataNode.Elements(dc + "identifier").Select(x => x.Value).ToList();
        metadata.Isbn = identifiers
            .Select(ExtractIsbnCandidate)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? identifiers.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        foreach (var subject in metadataNode.Elements(dc + "subject").Select(x => x.Value))
        {
            AddUnique(metadata.Subjects, subject);
        }

        var dateRaw = FirstNonEmpty(metadataNode.Elements(dc + "date").Select(x => x.Value), null);
        if (!string.IsNullOrWhiteSpace(dateRaw) && DateOnly.TryParse(dateRaw, out var parsedDate))
        {
            metadata.PublishedDate = parsedDate;
        }

        var metaNodes = metadataNode.Elements().Where(x => x.Name.LocalName == "meta").ToList();
        metadata.Series = metaNodes
            .FirstOrDefault(x => string.Equals(x.Attribute("name")?.Value, "calibre:series", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("content")?.Value
            ?.Trim();

        var seriesIndexRaw = metaNodes
            .FirstOrDefault(x => string.Equals(x.Attribute("name")?.Value, "calibre:series_index", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("content")?.Value;

        if (!string.IsNullOrWhiteSpace(seriesIndexRaw)
            && double.TryParse(seriesIndexRaw, out var seriesIndex))
        {
            metadata.SeriesIndex = seriesIndex;
        }
    }

    private static Dictionary<string, string> BuildManifestMap(XDocument opfDoc)
    {
        return opfDoc
            .Descendants()
            .Where(x => x.Name.LocalName == "item")
            .Select(x => new
            {
                Id = x.Attribute("id")?.Value,
                Href = x.Attribute("href")?.Value,
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
            .ToDictionary(x => x.Id!, x => x.Href!, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveCoverPath(XDocument opfDoc, Dictionary<string, string> manifestById)
    {
        var metadataNode = opfDoc.Descendants().FirstOrDefault(x => x.Name.LocalName == "metadata");
        var coverId = metadataNode?
            .Elements()
            .FirstOrDefault(x =>
                x.Name.LocalName == "meta"
                && string.Equals(x.Attribute("name")?.Value, "cover", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("content")?.Value;

        if (!string.IsNullOrWhiteSpace(coverId) && manifestById.TryGetValue(coverId, out var coverHref))
        {
            return coverHref;
        }

        return manifestById.Values.FirstOrDefault(x => ImageExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()));
    }

    private static IReadOnlyList<TextChunk> ExtractSpineTextChunks(
        ZipArchive archive,
        XDocument opfDoc,
        Dictionary<string, string> manifestById,
        string opfDirectory,
        List<string> warnings)
    {
        var textBuilder = new StringBuilder();

        var itemRefs = opfDoc
            .Descendants()
            .Where(x => x.Name.LocalName == "itemref")
            .Select(x => x.Attribute("idref")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();

        foreach (var idRef in itemRefs)
        {
            if (!manifestById.TryGetValue(idRef, out var href))
            {
                warnings.Add($"Spine entry '{idRef}' missing from manifest.");
                continue;
            }

            var entry = FindZipEntry(archive, ResolveZipPath(opfDirectory, href));
            if (entry is null)
            {
                warnings.Add($"Spine content '{href}' not found in archive.");
                continue;
            }

            try
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var raw = reader.ReadToEnd();
                var text = ExtractTextFromMarkup(raw);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textBuilder.AppendLine(text);
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to read spine file '{href}': {ex.Message}");
            }
        }

        if (textBuilder.Length == 0)
        {
            foreach (var entry in archive.Entries.Where(x => IsMarkupFile(x.FullName)))
            {
                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    var raw = reader.ReadToEnd();
                    var text = ExtractTextFromMarkup(raw);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textBuilder.AppendLine(text);
                    }
                }
                catch
                {
                    // ignore fallback parse failures
                }
            }
        }

        return TextChunker.Chunk(textBuilder.ToString());
    }

    private static bool IsMarkupFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".xhtml" or ".html" or ".htm" or ".xml";
    }

    private static string ExtractTextFromMarkup(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var noScript = Regex.Replace(raw, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var noTags = Regex.Replace(noScript, "<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        return decoded.Trim();
    }

    private static CoverImage? TryReadCover(ZipArchive archive, string zipPath, List<string> warnings)
    {
        var entry = FindZipEntry(archive, zipPath);
        if (entry is null)
        {
            warnings.Add($"Cover entry '{zipPath}' not found.");
            return null;
        }

        try
        {
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return new CoverImage(GetMimeType(entry.FullName), memory.ToArray(), entry.FullName);
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to extract cover '{entry.FullName}': {ex.Message}");
            return null;
        }
    }

    private static CoverImage? TryReadCoverFromFirstImage(ZipArchive archive, List<string> warnings)
    {
        var imageEntry = archive.Entries
            .Where(e => ImageExtensions.Contains(Path.GetExtension(e.FullName).ToLowerInvariant()))
            .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (imageEntry is null)
        {
            return null;
        }

        try
        {
            using var stream = imageEntry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return new CoverImage(GetMimeType(imageEntry.FullName), memory.ToArray(), imageEntry.FullName);
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to extract fallback cover '{imageEntry.FullName}': {ex.Message}");
            return null;
        }
    }

    private static ZipArchiveEntry? FindZipEntry(ZipArchive archive, string zipPath)
    {
        var normalized = zipPath.Replace('\\', '/').Trim('/');
        return archive.Entries.FirstOrDefault(x =>
            string.Equals(x.FullName.Replace('\\', '/').Trim('/'), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveZipPath(string baseDirectory, string relativePath)
    {
        var stack = new Stack<string>();

        foreach (var segment in baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            stack.Push(segment);
        }

        var baseParts = stack.Reverse().ToList();
        var parts = new List<string>(baseParts);

        foreach (var segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.RemoveAt(parts.Count - 1);
                }

                continue;
            }

            parts.Add(segment);
        }

        return string.Join('/', parts);
    }

    private static string? FirstNonEmpty(IEnumerable<string?> values, string? fallback)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? fallback;
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

    private static string? ExtractIsbnCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = Regex.Replace(value, "[^0-9Xx]", string.Empty);
        if (cleaned.Length is 10 or 13)
        {
            return cleaned.ToUpperInvariant();
        }

        return null;
    }

    private static string GetMimeType(string name)
    {
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };
    }
}
