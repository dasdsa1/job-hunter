using System.Net.Http;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// RemoteOK — free remote-jobs API, no auth (but needs a User-Agent header).
/// https://remoteok.com/api  — no server search param, so filter client-side.
/// First array element is a legal/metadata notice and is skipped.
/// </summary>
public class RemoteOkSource : IJobSource
{
    public string Id => "remoteok";

    public bool IsEnabled(SearchConfig config, AppConfig appConfig) =>
        config.Sites.Contains(Id, StringComparer.OrdinalIgnoreCase);

    public async Task<List<JobListing>> FetchAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        log.Report("RemoteOK: fetching https://remoteok.com/api");
        var json = await SourceHelpers.Http.GetStringAsync("https://remoteok.com/api", ct);
        var jobs = Parse(json, config);
        log.Report($"RemoteOK: {jobs.Count} job(s) after keyword filter");
        return jobs;
    }

    public static List<JobListing> Parse(string json, SearchConfig config)
    {
        var jobs = new List<JobListing>();
        var arr = JsonNode.Parse(json)?.AsArray();
        if (arr is null) return jobs;

        var keywords = SourceHelpers.Keywords(config);

        foreach (var j in arr)
        {
            if (j is null) continue;
            try
            {
                // Metadata/legal element has no "position" field.
                var title = j["position"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(title)) continue;

                string? salary = null;
                var min = j["salary_min"]?.AsValue().TryGetValue<double>(out var minVal) == true ? minVal : (double?)null;
                var max = j["salary_max"]?.AsValue().TryGetValue<double>(out var maxVal) == true ? maxVal : (double?)null;
                if (min.HasValue || max.HasValue)
                    salary = $"{min:N0}–{max:N0}";

                var job = new JobListing
                {
                    Id          = $"remoteok-{j["id"]?.ToString() ?? j["slug"]?.ToString() ?? Guid.NewGuid().ToString()}",
                    Title       = title.Trim(),
                    Company     = (j["company"]?.GetValue<string>() ?? "").Trim(),
                    Location    = (j["location"]?.GetValue<string>() ?? "Remote").Trim(),
                    Description = SourceHelpers.StripHtml(j["description"]?.GetValue<string>()),
                    Url         = j["url"]?.GetValue<string>() ?? "",
                    Source      = "remoteok",
                    PostedDate  = j["date"]?.GetValue<string>(),
                    Salary      = salary,
                };
                if (SourceHelpers.MatchesKeywords(job, keywords))
                    jobs.Add(job);
            }
            catch (Exception ex)
            {
                AppLogger.Exception("RemoteOkSource.Parse item", ex);
            }
        }
        return jobs.Take(config.MaxJobsPerSite).ToList();
    }
}
