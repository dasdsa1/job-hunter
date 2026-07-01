using System.Net.Http;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// Adzuna — broad job aggregator. Free API, needs app_id + app_key
/// (signup: developer.adzuna.com). Server-side search via what/where.
/// </summary>
public class AdzunaSource : IJobSource
{
    public string Id => "adzuna";

    public bool IsEnabled(SearchConfig config, AppConfig appConfig) =>
        config.Sites.Contains(Id, StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(appConfig.AdzunaAppId)
        && !string.IsNullOrWhiteSpace(appConfig.AdzunaAppKey);

    public async Task<List<JobListing>> FetchAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        var country = string.IsNullOrWhiteSpace(appConfig.AdzunaCountry) ? "us" : appConfig.AdzunaCountry.ToLowerInvariant();
        var what  = Uri.EscapeDataString($"{config.JobTitle} {config.Keywords}".Trim());
        var where = Uri.EscapeDataString(config.Location);
        var url =
            $"https://api.adzuna.com/v1/api/jobs/{country}/search/1" +
            $"?app_id={appConfig.AdzunaAppId}&app_key={appConfig.AdzunaAppKey}" +
            $"&results_per_page={config.MaxJobsPerSite}&what={what}&where={where}&content-type=application/json";
        log.Report($"Adzuna: fetching (country={country}, what='{config.JobTitle} {config.Keywords}')");
        var json = await SourceHelpers.Http.GetStringAsync(url, ct);
        var jobs = Parse(json, config);
        log.Report($"Adzuna: {jobs.Count} job(s)");
        return jobs;
    }

    /// <summary>Cheap one-result call to verify app_id/app_key work before a real run.</summary>
    public static async Task<(bool ok, string message)> TestConnectionAsync(AppConfig appConfig)
    {
        try
        {
            var country = string.IsNullOrWhiteSpace(appConfig.AdzunaCountry) ? "us" : appConfig.AdzunaCountry.ToLowerInvariant();
            var url =
                $"https://api.adzuna.com/v1/api/jobs/{country}/search/1" +
                $"?app_id={appConfig.AdzunaAppId}&app_key={appConfig.AdzunaAppKey}&results_per_page=1";
            var json = await SourceHelpers.Http.GetStringAsync(url);
            var count = JsonNode.Parse(json)?["count"]?.GetValue<long>();
            return (true, $"Connected — {count:N0} jobs available for country '{country}'");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Adzuna rejected the request: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static List<JobListing> Parse(string json, SearchConfig config)
    {
        var jobs = new List<JobListing>();
        var arr = JsonNode.Parse(json)?["results"]?.AsArray();
        if (arr is null) return jobs;

        var excludeKeywords = SourceHelpers.ExcludeKeywords(config);

        foreach (var j in arr)
        {
            if (j is null) continue;
            try
            {
                var title = j["title"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(title)) continue;

                string? salary = null;
                var min = TryGetDouble(j["salary_min"]);
                var max = TryGetDouble(j["salary_max"]);
                if (min.HasValue || max.HasValue)
                    salary = $"{min:N0}–{max:N0}";

                var job = new JobListing
                {
                    Id          = $"adzuna-{j["id"]?.ToString() ?? Guid.NewGuid().ToString()}",
                    Title       = SourceHelpers.StripHtml(title).Trim(),
                    Company     = (j["company"]?["display_name"]?.GetValue<string>() ?? "").Trim(),
                    Location    = (j["location"]?["display_name"]?.GetValue<string>() ?? "").Trim(),
                    Description = SourceHelpers.StripHtml(j["description"]?.GetValue<string>()),
                    Url         = j["redirect_url"]?.GetValue<string>() ?? "",
                    Source      = "adzuna",
                    PostedDate  = j["created"]?.GetValue<string>(),
                    Salary      = salary,
                };

                if (SourceHelpers.IsNotExcluded(job, excludeKeywords))
                    jobs.Add(job);
            }
            catch (Exception ex)
            {
                AppLogger.Exception("AdzunaSource.Parse item", ex);
            }
        }
        return jobs.Take(config.MaxJobsPerSite).ToList();
    }

    private static double? TryGetDouble(JsonNode? node) =>
        node is not null && node.AsValue().TryGetValue<double>(out var d) ? d : null;
}
