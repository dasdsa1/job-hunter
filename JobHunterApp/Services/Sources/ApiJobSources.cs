using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// Orchestrates all headless HTTP job sources: fetches every enabled source in
/// parallel, tolerates per-source failure, and dedups across sources.
/// </summary>
public static class ApiJobSources
{
    private static readonly IJobSource[] All =
    [
        new RemotiveSource(),
        new RemoteOkSource(),
        new ArbeitnowSource(),
        new AdzunaSource(),
    ];

    /// <summary>Source ids selectable in the UI / search config.</summary>
    public static IReadOnlyList<string> Ids => All.Select(s => s.Id).ToArray();

    /// <summary>True if any API source is selected — lets callers skip the browser entirely.</summary>
    public static bool AnyEnabled(SearchConfig config, AppConfig appConfig) =>
        All.Any(s => s.IsEnabled(config, appConfig));

    public static async Task<List<JobListing>> FetchAllAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        var enabled = All.Where(s => s.IsEnabled(config, appConfig)).ToList();
        if (enabled.Count == 0) return [];

        log.Report($"API sources: fetching {enabled.Count} in parallel ({string.Join(", ", enabled.Select(s => s.Id))})");

        var tasks = enabled.Select(s => SafeFetchAsync(s, config, appConfig, log, ct));
        var results = await Task.WhenAll(tasks);

        return Dedup(results.SelectMany(r => r));
    }

    private static async Task<List<JobListing>> SafeFetchAsync(
        IJobSource source, SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct)
    {
        try
        {
            return await source.FetchAsync(config, appConfig, log, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log.Report($"{source.Id}: failed — {ex.Message}");
            AppLogger.Exception($"ApiJobSources.{source.Id}", ex);
            return [];
        }
    }

    /// <summary>Dedup by exact Id, then by normalized title+company (cross-source repost).</summary>
    public static List<JobListing> Dedup(IEnumerable<JobListing> jobs)
    {
        var seenIds  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result   = new List<JobListing>();

        foreach (var j in jobs)
        {
            if (!seenIds.Add(j.Id)) continue;
            var key = $"{Norm(j.Title)}|{Norm(j.Company)}";
            if (key != "|" && !seenKeys.Add(key)) continue;
            result.Add(j);
        }
        return result;
    }

    private static string Norm(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
