using System.Text;
using AIJobCoach.Api.Modules.Resumes.Infrastructure;
using AIJobCoach.Api.Modules.Resumes.Shared;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AIJobCoach.Tests.Modules.Resumes.Infrastructure;

public sealed class ResumeTextExtractorTests
{
    private static ResumeTextExtractor CreateExtractor()
    {
        return new ResumeTextExtractor(NullLogger<ResumeTextExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnText_WhenPdfIsValid()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(CreatePdf("Backend engineer resume."));

        // Act
        var result = await extractor.ExtractAsync(stream, ResumeMimeTypes.Pdf);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("Backend engineer resume.");
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnText_WhenDocxIsValid()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(CreateDocx("First paragraph.", "Second paragraph."));

        // Act
        var result = await extractor.ExtractAsync(stream, ResumeMimeTypes.Docx);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("First paragraph.");
        result.Should().Contain("Second paragraph.");
        result!.Should().Contain("\n");
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenPdfBytesAreCorrupt()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a real pdf"));

        // Act
        var result = await extractor.ExtractAsync(stream, ResumeMimeTypes.Pdf);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenDocxBytesAreCorrupt()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a real docx"));

        // Act
        var result = await extractor.ExtractAsync(stream, ResumeMimeTypes.Docx);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldReturnNull_WhenMimeTypeIsUnsupported()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text"));

        // Act
        var result = await extractor.ExtractAsync(stream, "text/plain");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExtractAsync_ShouldThrow_WhenCancellationRequested()
    {
        // Arrange
        var extractor = CreateExtractor();
        using var stream = new MemoryStream(CreatePdf("Anything."));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => extractor.ExtractAsync(stream, ResumeMimeTypes.Pdf, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static byte[] CreatePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    private static byte[] CreateDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            foreach (var paragraph in paragraphs)
            {
                body.AppendChild(new Paragraph(new Run(new Text(paragraph))));
            }
        }

        return stream.ToArray();
    }
}
