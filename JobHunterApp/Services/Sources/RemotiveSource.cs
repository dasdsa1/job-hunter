using System.Net.Http;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>Remotive — free remote-jobs API, no auth. https://remotive.com/api/remote-jobs </summary>
public class RemotiveSource : IJobSource
{
    public string Id => "remotive";

    public bool IsEnabled(SearchConfig config, AppConfig appConfig) =>
        config.Sites.Contains(Id, StringComparer.OrdinalIgnoreCase);

    public async Task<List<JobListing>> FetchAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        var search = Uri.EscapeDataString($"{config.JobTitle} {config.Keywords}".Trim());
        var url = $"https://remotive.com/api/remote-jobs?search={search}&limit={config.MaxJobsPerSite}";
        log.Report($"Remotive: fetching {url}");
        var json = await SourceHelpers.Http.GetStringAsync(url, ct);
        var jobs = Parse(json, config);
        log.Report($"Remotive: {jobs.Count} job(s)");
        return jobs;
    }

    /// <summary>Pure parse — sample JSON in, JobListings out (unit-testable, no HTTP).</summary>
    public static List<JobListing> Parse(string json, SearchConfig config)
    {
        var jobs = new List<JobListing>();
        var arr = JsonNode.Parse(json)?["jobs"]?.AsArray();
        if (arr is null) return jobs;

        foreach (var j in arr)
        {
            if (j is null) continue;
            try
            {
                var title   = j["title"]?.GetValue<string>() ?? "";
                var company = j["company_name"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company)) continue;

                jobs.Add(new JobListing
                {
                    Id          = $"remotive-{j["id"]?.ToString() ?? Guid.NewGuid().ToString()}",
                    Title       = title.Trim(),
                    Company     = company.Trim(),
                    Location    = (j["candidate_required_location"]?.GetValue<string>() ?? "Remote").Trim(),
                    Description = SourceHelpers.StripHtml(j["description"]?.GetValue<string>()),
                    Url         = j["url"]?.GetValue<string>() ?? "",
                    Source      = "remotive",
                    PostedDate  = j["publication_date"]?.GetValue<string>(),
                    Salary      = j["salary"]?.GetValue<string>(),
                });
            }
            catch (Exception ex)
            {
                AppLogger.Exception("RemotiveSource.Parse item", ex);
            }
        }
        return jobs.Take(config.MaxJobsPerSite).ToList();
    }
}
