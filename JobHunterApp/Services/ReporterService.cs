using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class ReporterService
{
    public static string GenerateReport(
        SearchConfig config, int totalScraped, int totalMatched,
        IEnumerable<JobMatch> matches)
    {
        Directory.CreateDirectory(AppPaths.ReportsDir);
        var ts       = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss");
        var htmlPath = Path.Combine(AppPaths.ReportsDir, $"report-{ts}.html");
        var jsonPath = Path.Combine(AppPaths.ReportsDir, $"report-{ts}.json");

        var report = new
        {
            timestamp    = DateTime.UtcNow.ToString("o"),
            searchConfig = config,
            totalScraped,
            totalMatched,
            jobMatches   = matches
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(htmlPath, BuildHtml(config, totalScraped, totalMatched, matches));
        return htmlPath;
    }

    private static string BuildHtml(
        SearchConfig cfg, int totalScraped, int totalMatched,
        IEnumerable<JobMatch> matches)
    {
        var matchList = matches.ToList();
        var applied   = matchList.Where(m => m.Applied).ToList();

        string Row(JobMatch m, bool showLetter)
        {
            var colour = m.Match.Score >= 8 ? "#22c55e" : m.Match.Score >= 6 ? "#f59e0b" : "#ef4444";
            var letterCol = showLetter
                ? "<td><details><summary>View</summary><pre>" + Esc(m.CoverLetter ?? "") + "</pre></details></td>"
                : "";
            return "<tr>" +
                   "<td><a href=\"" + Esc(m.Job.Url) + "\" target=\"_blank\">" + Esc(m.Job.Title) + "</a></td>" +
                   "<td>" + Esc(m.Job.Company) + "</td>" +
                   "<td>" + Esc(m.Job.Location) + "</td>" +
                   "<td><span class=\"badge\" style=\"background:" + colour + "\">" + m.Match.Score + "/10</span></td>" +
                   "<td>" + Esc(m.Job.Source) + "</td>" +
                   "<td>" + Esc(m.Match.Summary) + "</td>" +
                   letterCol + "</tr>";
        }

        var appliedSection = applied.Count > 0
            ? "<h2>Applied Jobs (" + applied.Count + ")</h2>" +
              "<table><thead><tr><th>Title</th><th>Company</th><th>Location</th><th>Score</th><th>Source</th><th>Summary</th><th>Cover Letter</th></tr></thead>" +
              "<tbody>" + string.Join("", applied.Select(m => Row(m, true))) + "</tbody></table>"
            : "";

        var css =
            "body{font-family:system-ui,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#1e293b}" +
            "h1{color:#0f172a}h2{color:#1e40af;margin-top:2rem}" +
            "table{width:100%;border-collapse:collapse;margin-top:1rem;font-size:.9rem}" +
            "th{background:#1e293b;color:white;padding:.5rem .75rem;text-align:left}" +
            "td{padding:.5rem .75rem;border-bottom:1px solid #e2e8f0;vertical-align:top}" +
            ".badge{color:white;padding:.2rem .5rem;border-radius:9999px;font-weight:600}" +
            ".stat{display:inline-block;background:#f1f5f9;border-radius:.5rem;padding:1rem 1.5rem;margin:.5rem;text-align:center}" +
            ".stat-num{font-size:2rem;font-weight:700;color:#1e40af}" +
            "pre{white-space:pre-wrap;background:#f8fafc;padding:.75rem;border-radius:.25rem;font-size:.8rem;max-width:400px}" +
            "a{color:#2563eb}details summary{cursor:pointer;color:#2563eb}";

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\">" +
               "<title>Job Hunt Report</title><style>" + css + "</style></head><body>" +
               "<h1>Job Hunt Report</h1>" +
               "<p style=\"color:#64748b\">" + DateTime.UtcNow.ToString("O") + "</p>" +
               "<div>" +
               "<div class=\"stat\"><div class=\"stat-num\">" + totalScraped + "</div>Jobs scraped</div>" +
               "<div class=\"stat\"><div class=\"stat-num\">" + totalMatched + "</div>Matched (&ge;" + cfg.MinScore + ")</div>" +
               "<div class=\"stat\"><div class=\"stat-num\">" + applied.Count + "</div>Applied</div>" +
               "</div>" +
               appliedSection +
               "<h2>All Matched Jobs (" + totalMatched + ")</h2>" +
               "<table><thead><tr><th>Title</th><th>Company</th><th>Location</th><th>Score</th><th>Source</th><th>Summary</th></tr></thead>" +
               "<tbody>" + string.Join("", matchList.Select(m => Row(m, false))) + "</tbody></table>" +
               "</body></html>";
    }

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;")
        .Replace(">", "&gt;").Replace("\"", "&quot;");
}
