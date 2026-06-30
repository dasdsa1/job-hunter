using System.Net.Http;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// Arbeitnow — free job-board API, no auth. EU + remote roles.
/// https://www.arbeitnow.com/api/job-board-api  — no server search, filter client-side.
/// </summary>
public class ArbeitnowSource : IJobSource
{
    public string Id => "arbeitnow";

    public bool IsEnabled(SearchConfig config, AppConfig appConfig) =>
        config.Sites.Contains(Id, StringComparer.OrdinalIgnoreCase);

    public async Task<List<JobListing>> FetchAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        log.Report("Arbeitnow: fetching https://www.arbeitnow.com/api/job-board-api");
        var json = await SourceHelpers.Http.GetStringAsync(
            "https://www.arbeitnow.com/api/job-board-api", ct);
        var jobs = Parse(json, config);
        log.Report($"Arbeitnow: {jobs.Count} job(s) after keyword filter");
        return jobs;
    }

    public static List<JobListing> Parse(string json, SearchConfig config)
    {
        var jobs = new List<JobListing>();
        var arr = JsonNode.Parse(json)?["data"]?.AsArray();
        if (arr is null) return jobs;

        var keywords = SourceHelpers.Keywords(config);

        foreach (var j in arr)
        {
            if (j is null) continue;
            try
            {
                var title = j["title"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var job = new JobListing
                {
                    Id          = $"arbeitnow-{j["slug"]?.GetValue<string>() ?? Guid.NewGuid().ToString()}",
                    Title       = title.Trim(),
                    Company     = (j["company_name"]?.GetValue<string>() ?? "").Trim(),
                    Location    = (j["location"]?.GetValue<string>() ?? "").Trim(),
                    Description = SourceHelpers.StripHtml(j["description"]?.GetValue<string>()),
                    Url         = j["url"]?.GetValue<string>() ?? "",
                    Source      = "arbeitnow",
                    PostedDate  = j["created_at"]?.ToString(),
                };
                if (SourceHelpers.MatchesKeywords(job, keywords))
                    jobs.Add(job);
            }
            catch (Exception ex)
            {
                AppLogger.Exception("ArbeitnowSource.Parse item", ex);
            }
        }
        return jobs.Take(config.MaxJobsPerSite).ToList();
    }
}
