using System.Text.RegularExpressions;
using Bookdex.Core.Models;

namespace Bookdex.Core.Readers.Internal;

internal static class TextChunker
{
    public static IReadOnlyList<TextChunk> Chunk(string? text, int maxChunkLength = 1200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = Regex.Replace(text, "\\s+", " ").Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        var chunks = new List<TextChunk>();
        var start = 0;
        var chunkIndex = 0;

        while (start < normalized.Length)
        {
            var remaining = normalized.Length - start;
            var take = Math.Min(maxChunkLength, remaining);

            if (take < remaining)
            {
                var split = FindSplitPoint(normalized, start, take);
                if (split > 0)
                {
                    take = split;
                }
            }

            var segment = normalized.Substring(start, take).Trim();
            if (segment.Length > 0)
            {
                chunks.Add(new TextChunk(chunkIndex++, segment));
            }

            start += take;
            while (start < normalized.Length && char.IsWhiteSpace(normalized[start]))
            {
                start++;
            }
        }

        return chunks;
    }

    private static int FindSplitPoint(string text, int start, int length)
    {
        var end = start + length;

        for (var i = end - 1; i > start + 1; i--)
        {
            var c = text[i];
            if (c is '.' or '!' or '?' or ';' or ',')
            {
                return i - start + 1;
            }
        }

        for (var i = end - 1; i > start + 1; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i - start;
            }
        }

        return length;
    }
}
