using System.Text;
using System.Text.RegularExpressions;
using Bookdex.Core.Models;
using Bookdex.Core.Readers.Internal;

namespace Bookdex.Core.Readers.Pdf;

public sealed partial class PdfFormatReader : IBookFormatReader
{
    public string Format => "pdf";

    public bool CanRead(string path, ReadOnlySpan<byte> header)
    {
        if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (header.Length < 5)
        {
            return false;
        }

        return header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F' && header[4] == '-';
    }

    public BookReadResult Read(string path)
    {
        var warnings = new List<string>();
        var metadata = BookMetadata.CreateFallback(path);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            return BookReadResult.BestEffort(path, Format, [$"Failed to read PDF bytes: {ex.Message}"]);
        }

        var raw = Encoding.Latin1.GetString(bytes);

        metadata.Title = FirstNonEmpty(DecodePdfString(ExtractSingleValue(TitleRegex(), raw)), metadata.Title);

        var authorRaw = DecodePdfString(ExtractSingleValue(AuthorRegex(), raw));
        if (!string.IsNullOrWhiteSpace(authorRaw))
        {
            foreach (var author in authorRaw.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddUnique(metadata.Authors, author);
            }
        }

        metadata.Description = FirstNonEmpty(DecodePdfString(ExtractSingleValue(SubjectRegex(), raw)), metadata.Description);

        var keywords = DecodePdfString(ExtractSingleValue(KeywordsRegex(), raw));
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            foreach (var subject in keywords.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddUnique(metadata.Subjects, subject);
            }
        }

        var extractedText = new StringBuilder();

        foreach (Match match in TextShowRegex().Matches(raw))
        {
            var value = match.Groups["value"].Value;
            var decoded = DecodePdfString(value);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                extractedText.AppendLine(decoded);
            }
        }

        foreach (Match match in TextArrayRegex().Matches(raw))
        {
            var segment = match.Groups["value"].Value;
            foreach (Match item in TextArrayItemRegex().Matches(segment))
            {
                var decoded = DecodePdfString(item.Groups["value"].Value);
                if (!string.IsNullOrWhiteSpace(decoded))
                {
                    extractedText.Append(decoded);
                    extractedText.Append(' ');
                }
            }

            if (segment.Length > 0)
            {
                extractedText.AppendLine();
            }
        }

        var textChunks = TextChunker.Chunk(extractedText.ToString());
        if (textChunks.Count == 0)
        {
            warnings.Add("No text drawing operations were detected in PDF stream.");
        }

        warnings.Add("PDF cover extraction is not implemented in this initial core slice.");

        return new BookReadResult(path, Format, metadata, cover: null, textChunks, warnings);
    }

    private static string? ExtractSingleValue(Regex regex, string raw)
    {
        var match = regex.Match(raw);
        return match.Success ? match.Groups["value"].Value : null;
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

    private static string DecodePdfString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != '\\')
            {
                result.Append(c);
                continue;
            }

            if (i + 1 >= value.Length)
            {
                break;
            }

            var next = value[++i];
            switch (next)
            {
                case 'n':
                    result.Append('\n');
                    break;
                case 'r':
                    result.Append('\r');
                    break;
                case 't':
                    result.Append('\t');
                    break;
                case 'b':
                    result.Append('\b');
                    break;
                case 'f':
                    result.Append('\f');
                    break;
                case '(':
                case ')':
                case '\\':
                    result.Append(next);
                    break;
                default:
                    if (next is >= '0' and <= '7')
                    {
                        var octal = new StringBuilder().Append(next);
                        for (var j = 0; j < 2 && i + 1 < value.Length; j++)
                        {
                            var peek = value[i + 1];
                            if (peek is >= '0' and <= '7')
                            {
                                octal.Append(peek);
                                i++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        try
                        {
                            var numeric = Convert.ToInt32(octal.ToString(), 8);
                            result.Append((char)numeric);
                        }
                        catch
                        {
                            result.Append(octal);
                        }
                    }
                    else
                    {
                        result.Append(next);
                    }

                    break;
            }
        }

        return result.ToString().Trim();
    }

    [GeneratedRegex(@"/Title\s*\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"/Author\s*\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex AuthorRegex();

    [GeneratedRegex(@"/Subject\s*\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex SubjectRegex();

    [GeneratedRegex(@"/Keywords\s*\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex KeywordsRegex();

    [GeneratedRegex(@"\((?<value>(?:\\.|[^\\)])*)\)\s*Tj", RegexOptions.Singleline)]
    private static partial Regex TextShowRegex();

    [GeneratedRegex(@"\[(?<value>.*?)\]\s*TJ", RegexOptions.Singleline)]
    private static partial Regex TextArrayRegex();

    [GeneratedRegex(@"\((?<value>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex TextArrayItemRegex();
}
