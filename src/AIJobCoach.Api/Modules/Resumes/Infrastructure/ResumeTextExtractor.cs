using System.Text;
using AIJobCoach.Api.Modules.Resumes.Application;
using AIJobCoach.Api.Modules.Resumes.Shared;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AIJobCoach.Api.Modules.Resumes.Infrastructure;

public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    private readonly ILogger<ResumeTextExtractor> _logger;
    public ResumeTextExtractor(ILogger<ResumeTextExtractor> logger)
    {
        _logger = logger;
    }
    
    public async Task<string?> ExtractAsync(
        Stream stream, 
        string mimeType, 
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return mimeType switch
            {
                ResumeMimeTypes.Pdf =>
                    await ExtractPdfAsync(stream, cancellationToken),
                ResumeMimeTypes.Docx =>
                    await ExtractDocxAsync(stream, cancellationToken),
                _ => null
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex, "Text extraction failed for resume file.");
            return null;
        }
    }
    
    private static Task<string?> ExtractPdfAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(stream);
        foreach (Page page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                builder.AppendLine(pageText);
            }
        }

        return Task.FromResult(Normalize(builder.ToString()));
    }

    private static Task<string?> ExtractDocxAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        using var document = WordprocessingDocument.Open(stream, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is not null)
        {
            foreach (Paragraph paragraph in body.Descendants<Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var paragraphText = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    builder.AppendLine(paragraphText);
                }
            }
        }

        return Task.FromResult(Normalize(builder.ToString()));
    }

    private static string? Normalize(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}

