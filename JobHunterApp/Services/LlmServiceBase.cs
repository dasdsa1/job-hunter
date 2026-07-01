using System.Text.Json;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>Shared prompts + parsing for any chat-completion-style LLM provider. Subclasses only
/// implement the HTTP transport (JSON/text/streaming generation).</summary>
public abstract class LlmServiceBase(RateLimiter rateLimiter) : ILlmService
{
    protected abstract Task<string> GenerateJsonAsync(string prompt, object? responseSchema = null);
    protected abstract Task<string> GenerateTextAsync(string prompt);
    protected abstract Task<string> GenerateStreamingAsync(string prompt, Action<string>? onChunk);

    public async Task<(bool ok, string message)> TestConnectionAsync()
    {
        try
        {
            var text = await GenerateTextAsync("Reply with exactly: OK");
            return string.IsNullOrWhiteSpace(text)
                ? (false, "Connected, but got an empty response. Check the model name.")
                : (true, $"Connected — model responded ({text.Trim()[..Math.Min(text.Trim().Length, 30)]})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<string> ExtractProfileAsync(string resume)
    {
        var prompt = $"""
            Condense the resume below into a compact candidate profile for a recruiter to
            skim before scoring job matches. Cover: seniority level, core skills/technologies,
            years of experience, domain(s) worked in, and notable roles. Plain text, no
            markdown, under 200 words.

            Resume:
            ---
            {resume}
            ---
            """;

        await rateLimiter.ThrottleAsync();
        return await GenerateTextAsync(prompt);
    }

    public async Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string profile,
        IProgress<(int current, int total)>? progress = null)
    {
        var jobList = jobs.ToList();
        var result = new Dictionary<string, MatchResult>();

        const int batchSize = 20;
        var batches = jobList
            .Select((job, index) => (job, index))
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.job).ToList())
            .ToList();

        AppLogger.Info($"Scoring {jobList.Count} jobs in {batches.Count} batch(es)");

        var responseSchema = new
        {
            type = "array",
            items = new
            {
                type = "object",
                properties = new
                {
                    id = new { type = "string" },
                    score = new { type = "integer", minimum = 1, maximum = 10 },
                    summary = new { type = "string" },
                    reasons = new { type = "array", items = new { type = "string" } },
                    redFlags = new { type = "array", items = new { type = "string" } }
                },
                required = new[] { "id", "score", "summary", "reasons" }
            }
        };

        for (var batchIdx = 0; batchIdx < batches.Count; batchIdx++)
        {
            var batch = batches[batchIdx];
            progress?.Report((batchIdx, batches.Count));
            AppLogger.Info($"Scoring batch {batchIdx + 1}/{batches.Count} ({batch.Count} jobs)");

            var snippets = batch.Select(j => new
            {
                id          = j.Id,
                title       = j.Title,
                company     = j.Company,
                location    = j.Location,
                salary      = j.Salary ?? "not listed",
                description = j.Description[..Math.Min(j.Description.Length, 2_500)]
            });

            var prompt = $$"""
                You are an experienced HR consultant advising a candidate one-on-one. You've
                read their resume closely and you're now reviewing a batch of job listings for
                them — the way a good recruiter would, not a keyword matcher. Be direct and
                specific, like you're talking to them, not filling out a form.

                Scoring guide:
                  9-10: Near-perfect fit - skills, seniority, and domain all align
                  7-8: Strong match - most requirements met, minor gaps
                  5-6: Moderate - roughly half the requirements match
                  3-4: Weak - significant skill or experience gaps
                  1-2: Poor fit

                Each job includes a salary field when the source disclosed one. Factor it into
                your take when present (e.g. below-market for the seniority/skills involved), and
                flag it as a red flag if it's missing entirely or vague — don't invent a number.

                Candidate profile:
                ---
                {{profile}}
                ---

                Jobs to evaluate (JSON):
                {{JsonSerializer.Serialize(snippets)}}

                For each job, write like you're briefing the candidate directly: what you'd tell
                them to push them toward or away from this role, and what to watch out for (vague
                comp, generic "rockstar ninja" language, scope creep in the listing, staffing-agency
                reposts, etc.) if anything looks off. Skip red flags entirely if there are none —
                don't invent problems.

                Return a JSON array (one object per job):
                [
                  {
                    "id": "<job id>",
                    "score": <1-10 integer>,
                    "summary": "<one sentence, written to the candidate, e.g. 'I'd push you toward this one — your backend depth covers most of it.'>",
                    "reasons": ["<concrete reason 1>", "<concrete reason 2>", "<concrete reason 3>"],
                    "redFlags": ["<optional: only if something genuinely looks off>"]
                  }
                ]
                """;

            await rateLimiter.ThrottleAsync();
            var raw = await GenerateJsonAsync(prompt, responseSchema);

            try
            {
                var arr = JsonNode.Parse(raw)?.AsArray();
                if (arr is not null)
                {
                    foreach (var item in arr)
                    {
                        var id      = item?["id"]?.GetValue<string>() ?? "";
                        var score   = item?["score"]?.GetValue<int>() ?? 0;
                        var summary = item?["summary"]?.GetValue<string>() ?? "";
                        var reasons = item?["reasons"]?.AsArray()
                            .Select(r => r?.GetValue<string>() ?? "").ToList() ?? [];
                        var redFlags = item?["redFlags"]?.AsArray()?
                            .Select(r => r?.GetValue<string>() ?? "").ToList() ?? [];
                        result[id] = new MatchResult
                        {
                            Score    = Math.Clamp(score, 1, 10),
                            Summary  = summary,
                            Reasons  = reasons,
                            RedFlags = redFlags
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Exception($"MatchJobsAsync batch parse {batchIdx + 1}", ex);
            }
        }

        return result;
    }

    public async Task<string> GenerateCoverLetterAsync(
        JobListing job, MatchResult match, string resume,
        IEnumerable<string> letterSnippets,
        Action<string>? onChunk = null)
    {
        var lettersSection = letterSnippets.Any()
            ? "\nRecommendation letter excerpts:\n" + string.Join("\n",
                letterSnippets.Select((s, i) => $"[Letter {i + 1}]: {s[..Math.Min(s.Length, 400)]}"))
            : "";

        var prompt = $"""
            Write a compelling, concise cover letter for the following job application.

            Job Title: {job.Title}
            Company: {job.Company}
            Location: {job.Location}
            Why I'm a good fit: {match.Summary}
            Key matching reasons: {string.Join("; ", match.Reasons)}
            {lettersSection}

            Job Description (excerpt):
            {job.Description[..Math.Min(job.Description.Length, 1_500)]}

            My Resume:
            ---
            {resume}
            ---

            Instructions:
            - 3 short paragraphs, under 280 words total
            - First paragraph: genuine excitement for THIS specific role/company
            - Second paragraph: 2-3 concrete skills from my resume that match the requirements
            - Third paragraph: brief closing with availability and enthusiasm
            - Use first person, active voice
            - Do NOT include placeholders like [Your Name] — write it as a finished letter
            - Do NOT add a subject line, date, or address block — just the body paragraphs
            """;

        await rateLimiter.ThrottleAsync();
        return await GenerateStreamingAsync(prompt, onChunk);
    }

    public async Task<(string cv, List<string> verificationIssues)> TailorCvAsync(
        string originalCv, JobListing job, MatchResult match)
    {
        var prompt = $"""
            You are a professional CV writer. Rewrite the candidate's CV to better match the job listing below.

            Rules:
            - Keep all information factually accurate — do NOT invent experience or skills
            - Reorder bullet points and sections to highlight the most relevant experience first
            - Rephrase descriptions to mirror the language used in the job posting
            - Remove or de-emphasise experience unrelated to this role
            - Keep the same general structure (sections like Experience, Education, Skills)
            - Output ONLY the CV text, no commentary, no markdown code fences

            Job Title: {job.Title}
            Company: {job.Company}
            Why candidate matches: {match.Summary}
            Key matching points: {string.Join("; ", match.Reasons)}

            Job Description:
            {job.Description[..Math.Min(job.Description.Length, 2_000)]}

            Original CV:
            ---
            {originalCv}
            ---
            """;

        await rateLimiter.ThrottleAsync();
        var tailoredCv = await GenerateTextAsync(prompt);

        // P0 #5: Verify tailored CV doesn't hallucinate skills/roles/companies
        var (valid, issues) = CvVerificationService.VerifyTailoredCv(originalCv, tailoredCv);
        if (!valid)
        {
            AppLogger.Warn($"CV tailoring verification found issues for {job.Company}/{job.Title}: {string.Join("; ", issues)}");
        }

        return (tailoredCv, issues);
    }
}
