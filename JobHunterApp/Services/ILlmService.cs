using JobHunterApp.Models;
using PlatformSchemas;

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

    /// <summary>Extracts a structured CBIF Curriculum from raw resume text, for cross-app
    /// interchange (platform-schemas). Separate from ExtractProfileAsync, which produces a
    /// free-text summary tuned for job-match scoring prompts, not for storage/interop.</summary>
    Task<Curriculum> ExtractCurriculumAsync(string resume);

    Task<string> GenerateCoverLetterAsync(
        JobListing job, MatchResult match, string resume,
        IEnumerable<string> letterSnippets,
        Action<string>? onChunk = null);

    Task<(string cv, List<string> verificationIssues)> TailorCvAsync(string originalCv, JobListing job, MatchResult match);
}
