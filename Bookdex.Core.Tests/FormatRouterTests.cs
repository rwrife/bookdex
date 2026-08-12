using System.IO.Compression;
using System.Text;
using Bookdex.Core.Readers;

namespace Bookdex.Core.Tests;

public sealed class FormatRouterTests
{
    [Fact]
    public void Read_Epub_ExtractsMetadataCoverAndText()
    {
        var tempDir = CreateTempDir();
        var epubPath = Path.Combine(tempDir, "sample.epub");
        CreateSampleEpub(epubPath);

        var router = FormatRouter.CreateDefault();
        var result = router.Read(epubPath);

        Assert.Equal("epub", result.Format);
        Assert.Equal("Sample EPUB", result.Metadata.Title);
        Assert.Contains("Author One", result.Metadata.Authors);
        Assert.Equal("Fiction", result.Metadata.Subjects.Single());
        Assert.NotNull(result.Cover);
        Assert.NotEmpty(result.TextChunks);
        Assert.Contains(result.TextChunks, c => c.Text.Contains("chapter one", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_Cbz_ExtractsComicInfoAndCover()
    {
        var tempDir = CreateTempDir();
        var cbzPath = Path.Combine(tempDir, "sample.cbz");
        CreateSampleCbz(cbzPath);

        var router = FormatRouter.CreateDefault();
        var result = router.Read(cbzPath);

        Assert.Equal("cbz", result.Format);
        Assert.Equal("Sample Comic", result.Metadata.Title);
        Assert.Equal("Demo Series", result.Metadata.Series);
        Assert.Equal(1d, result.Metadata.SeriesIndex);
        Assert.Contains("Jane Writer", result.Metadata.Authors);
        Assert.NotNull(result.Cover);
        Assert.Equal("image/jpeg", result.Cover!.MimeType);
    }

    [Fact]
    public void Read_Pdf_ExtractsBasicMetadataAndText()
    {
        var tempDir = CreateTempDir();
        var pdfPath = Path.Combine(tempDir, "sample.pdf");
        CreateSamplePdf(pdfPath);

        var router = FormatRouter.CreateDefault();
        var result = router.Read(pdfPath);

        Assert.Equal("pdf", result.Format);
        Assert.Equal("Sample PDF", result.Metadata.Title);
        Assert.Contains("Ada Lovelace", result.Metadata.Authors);
        Assert.Contains(result.Metadata.Subjects, x => x == "science");
        Assert.NotEmpty(result.TextChunks);
        Assert.Contains(result.TextChunks, c => c.Text.Contains("Hello PDF World", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_MalformedFile_ReturnsWarningsInsteadOfThrowing()
    {
        var tempDir = CreateTempDir();
        var malformedPath = Path.Combine(tempDir, "broken.epub");
        File.WriteAllBytes(malformedPath, [1, 2, 3, 4, 5]);

        var router = FormatRouter.CreateDefault();
        var result = router.Read(malformedPath);

        Assert.Equal("epub", result.Format);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal("broken", result.Metadata.Title);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bookdex-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateSampleEpub(string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        var mimetype = archive.CreateEntry("mimetype");
        using (var writer = new StreamWriter(mimetype.Open(), Encoding.UTF8))
        {
            writer.Write("application/epub+zip");
        }

        var container = archive.CreateEntry("META-INF/container.xml");
        using (var writer = new StreamWriter(container.Open(), Encoding.UTF8))
        {
            writer.Write("""
            <?xml version="1.0"?>
            <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
              <rootfiles>
                <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
              </rootfiles>
            </container>
            """);
        }

        var opf = archive.CreateEntry("OEBPS/content.opf");
        using (var writer = new StreamWriter(opf.Open(), Encoding.UTF8))
        {
            writer.Write("""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="2.0" unique-identifier="BookId">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:title>Sample EPUB</dc:title>
                <dc:creator>Author One</dc:creator>
                <dc:identifier id="BookId">ISBN:9781234567897</dc:identifier>
                <dc:language>en</dc:language>
                <dc:subject>Fiction</dc:subject>
                <dc:description>Small fixture epub.</dc:description>
                <meta name="cover" content="cover-image" />
              </metadata>
              <manifest>
                <item id="chapter-1" href="chapter1.xhtml" media-type="application/xhtml+xml"/>
                <item id="cover-image" href="cover.jpg" media-type="image/jpeg"/>
              </manifest>
              <spine>
                <itemref idref="chapter-1"/>
              </spine>
            </package>
            """);
        }

        var chapter = archive.CreateEntry("OEBPS/chapter1.xhtml");
        using (var writer = new StreamWriter(chapter.Open(), Encoding.UTF8))
        {
            writer.Write("""
            <html xmlns="http://www.w3.org/1999/xhtml">
              <body>
                <h1>Chapter One</h1>
                <p>This is a tiny EPUB fixture.</p>
              </body>
            </html>
            """);
        }

        var cover = archive.CreateEntry("OEBPS/cover.jpg");
        using (var stream = cover.Open())
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04 };
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static void CreateSampleCbz(string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        var comicInfo = archive.CreateEntry("ComicInfo.xml");
        using (var writer = new StreamWriter(comicInfo.Open(), Encoding.UTF8))
        {
            writer.Write("""
            <ComicInfo>
              <Title>Sample Comic</Title>
              <Series>Demo Series</Series>
              <Number>1</Number>
              <Writer>Jane Writer</Writer>
              <Publisher>Indie House</Publisher>
              <LanguageISO>en</LanguageISO>
              <Genre>Sci-Fi,Drama</Genre>
              <Summary>A tiny CBZ fixture with metadata.</Summary>
              <Year>2025</Year>
              <Month>1</Month>
              <Day>15</Day>
            </ComicInfo>
            """);
        }

        var cover = archive.CreateEntry("001.jpg");
        using (var stream = cover.Open())
        {
            var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x10, 0x20, 0x30, 0x40 };
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private static void CreateSamplePdf(string outputPath)
    {
        var pdf = """
        %PDF-1.4
        1 0 obj
        << /Type /Catalog /Pages 2 0 R >>
        endobj
        2 0 obj
        << /Type /Pages /Kids [3 0 R] /Count 1 >>
        endobj
        3 0 obj
        << /Type /Page /Parent 2 0 R /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>
        endobj
        4 0 obj
        << /Length 45 >>
        stream
        BT
        /F1 24 Tf
        72 720 Td
        (Hello PDF World) Tj
        ET
        endstream
        endobj
        5 0 obj
        << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
        endobj
        6 0 obj
        << /Title (Sample PDF) /Author (Ada Lovelace) /Keywords (science,fiction) >>
        endobj
        trailer
        << /Root 1 0 R /Info 6 0 R >>
        %%EOF
        """;

        File.WriteAllText(outputPath, pdf, Encoding.Latin1);
    }
}
