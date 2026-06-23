using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class CvTailorService
{
    public static string SaveAsDocx(string cvText, JobListing job)
    {
        Directory.CreateDirectory(AppPaths.TailoredCvsDir);
        var safeName = string.Concat((job.Company + "-" + job.Title)
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '))
            .Trim()[..Math.Min(60, (job.Company + "-" + job.Title).Length)];
        var outPath = Path.Combine(AppPaths.TailoredCvsDir, $"CV-{safeName}.docx");

        using var doc = WordprocessingDocument.Create(outPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body!;

        foreach (var line in cvText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                body.Append(new Paragraph());
                continue;
            }

            var isHeading = trimmed == trimmed.ToUpperInvariant() && trimmed.Length > 2;
            var isBullet  = trimmed.StartsWith('•') || trimmed.StartsWith('-') || trimmed.StartsWith('*');
            var text      = isBullet ? trimmed.TrimStart('•', '-', '*', ' ') : trimmed;

            var run  = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            if (isHeading) run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "26" });

            var para = new Paragraph(run);
            if (isBullet)
                para.ParagraphProperties = new ParagraphProperties(
                    new NumberingProperties(
                        new NumberingLevelReference { Val = 0 },
                        new NumberingId { Val = 1 }));

            body.Append(para);
        }

        mainPart.Document.Save();
        return outPath;
    }
}
