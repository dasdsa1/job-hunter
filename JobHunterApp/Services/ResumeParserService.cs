using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace JobHunterApp.Services;

public static class ResumeParserService
{
    public static async Task<string> ParseAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => await Task.Run(() => ParsePdf(filePath)),
            ".docx" => await Task.Run(() => ParseDocx(filePath)),
            _       => throw new NotSupportedException($"Unsupported file type: {ext}")
        };
    }

    private static string ParsePdf(string filePath)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(filePath);
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString().Trim();
    }

    private static string ParseDocx(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        return doc.MainDocumentPart?.Document?.Body?.InnerText?.Trim() ?? "";
    }
}
