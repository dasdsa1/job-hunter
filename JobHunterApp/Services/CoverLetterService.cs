using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class CoverLetterService
{
    public static string SaveAsDocx(string letterText, JobListing job)
    {
        Directory.CreateDirectory(AppPaths.CoverLettersDir);
        var safeName = string.Concat((job.Company + "-" + job.Title)
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '))
            .Trim()[..Math.Min(60, (job.Company + "-" + job.Title).Length)];
        var outPath = Path.Combine(AppPaths.CoverLettersDir, $"CoverLetter-{safeName}.docx");

        using var doc = WordprocessingDocument.Create(outPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body!;

        foreach (var line in letterText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                body.Append(new Paragraph());
                continue;
            }

            var run = new Run(new Text(trimmed) { Space = SpaceProcessingModeValues.Preserve });
            var para = new Paragraph(run);
            body.Append(para);
        }

        mainPart.Document.Save();
        return outPath;
    }
}
