using JobHunterApp.Models;

namespace JobHunterApp.Services.Sources;

/// <summary>
/// A headless, fully-automated job source backed by an HTTP/JSON API.
/// No browser, no login — runnable from the Worker or GUI without interaction.
/// </summary>
public interface IJobSource
{
    /// <summary>Stable id used in SearchConfig.Sites (e.g. "remotive").</summary>
    string Id { get; }

    /// <summary>True when this source is selected and has whatever credentials it needs.</summary>
    bool IsEnabled(SearchConfig config, AppConfig appConfig);

    Task<List<JobListing>> FetchAsync(
        SearchConfig config, AppConfig appConfig, IProgress<string> log, CancellationToken ct);
}
