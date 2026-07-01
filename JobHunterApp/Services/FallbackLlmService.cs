using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>Tries each provider in order; on failure (rate limit, outage, bad key — anything that
/// throws after the inner service's own retries are exhausted) moves to the next.</summary>
public class FallbackLlmService(IReadOnlyList<(LlmProvider provider, ILlmService service)> chain) : ILlmService
{
    public async Task<(bool ok, string message)> TestConnectionAsync() =>
        await RunAsync(s => s.TestConnectionAsync());

    public async Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string resume) =>
        await RunAsync(s => s.MatchJobsAsync(jobs, resume));

    public async Task<string> ExtractProfileAsync(string resume) =>
        await RunAsync(s => s.ExtractProfileAsync(resume));

    public async Task<string> GenerateCoverLetterAsync(
        JobListing job, MatchResult match, string resume,
        IEnumerable<string> letterSnippets, Action<string>? onChunk = null) =>
        await RunAsync(s => s.GenerateCoverLetterAsync(job, match, resume, letterSnippets, onChunk));

    public async Task<string> TailorCvAsync(string originalCv, JobListing job, MatchResult match) =>
        await RunAsync(s => s.TailorCvAsync(originalCv, job, match));

    private async Task<T> RunAsync<T>(Func<ILlmService, Task<T>> call)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            var (provider, service) = chain[i];
            try
            {
                return await call(service);
            }
            catch (Exception ex) when (i < chain.Count - 1)
            {
                AppLogger.Info($"{provider} failed, falling back to {chain[i + 1].provider}: {ex.Message}");
            }
        }
        // Unreachable if chain is non-empty — last attempt's exception propagates from the catch above.
        throw new InvalidOperationException("No LLM provider configured.");
    }
}
