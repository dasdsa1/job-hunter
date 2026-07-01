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

    /// <summary>Dedup by: 1) exact ID, 2) exact normalized title+company, 3) Levenshtein distance on title+company (fuzzy cross-source reposts).</summary>
    public static List<JobListing> Dedup(IEnumerable<JobListing> jobs)
    {
        var seenIds  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen     = new List<(string normalizedTitle, string normalizedCompany)>();
        var result   = new List<JobListing>();

        const double fuzzyThreshold = 0.85; // 85% similarity = likely duplicate

        foreach (var j in jobs)
        {
            if (!seenIds.Add(j.Id)) continue;

            var normTitle   = Norm(j.Title);
            var normCompany = Norm(j.Company);
            var key         = $"{normTitle}|{normCompany}";

            if (key != "|" && !seenKeys.Add(key)) continue;

            if (IsFuzzyDuplicate(normTitle, normCompany, seen, fuzzyThreshold)) continue;

            seen.Add((normTitle, normCompany));
            result.Add(j);
        }
        return result;
    }

    private static bool IsFuzzyDuplicate(
        string normalizedTitle, string normalizedCompany,
        List<(string title, string company)> seen, double threshold)
    {
        foreach (var (seenTitle, seenCompany) in seen)
        {
            if (normalizedCompany != seenCompany) continue;
            var similarity = LevenshteinSimilarity(normalizedTitle, seenTitle);
            if (similarity >= threshold) return true;
        }
        return false;
    }

    /// <summary>Returns 0.0–1.0, where 1.0 is exact match.</summary>
    private static double LevenshteinSimilarity(string a, string b)
    {
        var distance = LevenshteinDistance(a, b);
        var maxLen   = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var alen = a.Length;
        var blen = b.Length;
        if (alen == 0) return blen;
        if (blen == 0) return alen;

        var prev = new int[blen + 1];
        var curr = new int[blen + 1];

        for (var j = 0; j <= blen; j++) prev[j] = j;

        for (var i = 1; i <= alen; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= blen; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[blen];
    }

    private static string Norm(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
