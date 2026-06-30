using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// Shared HTTP client + text helpers for API job sources.
/// A single HttpClient is reused across all sources (socket reuse, no exhaustion).
/// </summary>
public static class SourceHelpers
{
    // Some boards (RemoteOK) reject requests without a normal User-Agent.
    public static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) JobHunterApp/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return c;
    }

    private static readonly Regex TagRx     = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SpacesRx  = new("[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex BlankRx   = new(@"\n[ \t]*\n[ \t\n]*", RegexOptions.Compiled);

    /// <summary>Strip HTML tags + decode entities so descriptions score cleanly.</summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = TagRx.Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r", "");
        text = SpacesRx.Replace(text, " ");      // collapse runs left by adjacent tags
        text = BlankRx.Replace(text, "\n\n");    // collapse blank-line runs
        return text.Trim();
    }

    /// <summary>Keyword tokens from the search (title + free keywords), lowercased.</summary>
    public static string[] Keywords(SearchConfig config) =>
        $"{config.JobTitle} {config.Keywords}"
            .Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToArray();

    /// <summary>
    /// Client-side relevance filter for sources without a server search param.
    /// Matches when ANY keyword token appears in title/company/description.
    /// Empty keyword set ⇒ keep everything.
    /// </summary>
    public static bool MatchesKeywords(JobListing job, string[] keywords)
    {
        if (keywords.Length == 0) return true;
        var hay = $"{job.Title} {job.Company} {job.Description}".ToLowerInvariant();
        return keywords.Any(hay.Contains);
    }
}
