using JobHunterApp.Models;

namespace JobHunterApp.Services;

public interface ILlmService
{
    Task<(bool ok, string message)> TestConnectionAsync();

    Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string resume);

    Task<string> GenerateCoverLetterAsync(
        JobListing job, MatchResult match, string resume,
        IEnumerable<string> letterSnippets,
        Action<string>? onChunk = null);

    Task<string> TailorCvAsync(string originalCv, JobListing job, MatchResult match);
}
