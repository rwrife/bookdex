namespace Bookdex.Core.Models;

public sealed record CoverImage(string MimeType, byte[] Data, string Source);
