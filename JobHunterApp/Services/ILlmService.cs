using JobHunterApp.Models;

namespace JobHunterApp.Services;

public interface ILlmService
{
    Task<(bool ok, string message)> TestConnectionAsync();

    Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string resume,
        IProgress<(int current, int total)>? progress = null);

    /// <summary>Condenses a resume into a short skills/experience summary, once, so callers can
    /// reuse it across many scoring batches instead of resending the full resume text each time.</summary>
    Task<string> ExtractProfileAsync(string resume);

    Task<string> GenerateCoverLetterAsync(
        JobListing job, MatchResult match, string resume,
        IEnumerable<string> letterSnippets,
        Action<string>? onChunk = null);

    Task<(string cv, List<string> verificationIssues)> TailorCvAsync(string originalCv, JobListing job, MatchResult match);
}
