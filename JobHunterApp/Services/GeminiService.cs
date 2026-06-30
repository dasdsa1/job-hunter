using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public class GeminiService(string apiKey, string model, RateLimiter rateLimiter)
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };
    private readonly string _baseUrl =
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}";
    private const int MaxRetries = 4;
    private static readonly int[] BackoffMs = { 2000, 4000, 8000, 16000 };

    public async Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string resume)
    {
        var jobList = jobs.ToList();
        var result = new Dictionary<string, MatchResult>();

        const int batchSize = 10;
        var batches = jobList
            .Select((job, index) => (job, index))
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.job).ToList())
            .ToList();

        AppLogger.Info($”Scoring {jobList.Count} jobs in {batches.Count} batch(es)”);

        for (var batchIdx = 0; batchIdx < batches.Count; batchIdx++)
        {
            var batch = batches[batchIdx];
            AppLogger.Info($”Scoring batch {batchIdx + 1}/{batches.Count} ({batch.Count} jobs)”);

            var snippets = batch.Select(j => new
            {
                id          = j.Id,
                title       = j.Title,
                company     = j.Company,
                location    = j.Location,
                description = j.Description[..Math.Min(j.Description.Length, 2_500)]
            });

            var prompt = $$”””
                You are a professional job application assistant.
                Evaluate each job listing against the candidate's resume and assign a match score.

                Scoring guide:
                  9-10: Near-perfect fit - skills, seniority, and domain all align
                  7-8: Strong match - most requirements met, minor gaps
                  5-6: Moderate - roughly half the requirements match
                  3-4: Weak - significant skill or experience gaps
                  1-2: Poor fit

                Resume:
                ---
                {{resume}}
                ---

                Jobs to evaluate (JSON):
                {{JsonSerializer.Serialize(snippets)}}

                Return a JSON array (one object per job):
                [
                  {
                    “id”: “<job id>”,
                    “score”: <1-10 integer>,
                    “summary”: “<one sentence explaining the score>”,
                    “reasons”: [“<reason 1>”, “<reason 2>”, “<reason 3>”]
                  }
                ]
                “””;

            await rateLimiter.ThrottleAsync();
            var raw = await GenerateJsonAsync(prompt);

            try
            {
                var arr = JsonNode.Parse(raw)?.AsArray();
                if (arr is not null)
                {
                    foreach (var item in arr)
                    {
                        var id      = item?[“id”]?.GetValue<string>() ?? “”;
                        var score   = item?[“score”]?.GetValue<int>() ?? 0;
                        var summary = item?[“summary”]?.GetValue<string>() ?? “”;
                        var reasons = item?[“reasons”]?.AsArray()
                            .Select(r => r?.GetValue<string>() ?? “”).ToList() ?? [];
                        result[id] = new MatchResult
                        {
                            Score   = Math.Clamp(score, 1, 10),
                            Summary = summary,
                            Reasons = reasons
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Exception($”MatchJobsAsync batch parse {batchIdx + 1}”, ex);
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
            - Do NOT include placeholders like [Your Name] â€” write it as a finished letter
            - Do NOT add a subject line, date, or address block â€” just the body paragraphs
            """;

        await rateLimiter.ThrottleAsync();
        return await GenerateStreamingAsync(prompt, onChunk);
    }

    public async Task<string> TailorCvAsync(
        string originalCv, JobListing job, MatchResult match)
    {
        var prompt = $"""
            You are a professional CV writer. Rewrite the candidate's CV to better match the job listing below.

            Rules:
            - Keep all information factually accurate â€” do NOT invent experience or skills
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
        return await GenerateTextAsync(prompt);
    }

    // â”€â”€ HTTP helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<string> GenerateJsonAsync(string prompt)
    {
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };
        var response = await PostAsync(body, stream: false);
        return ExtractText(response);
    }

    private async Task<string> GenerateTextAsync(string prompt)
    {
        var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
        var response = await PostAsync(body, stream: false);
        return ExtractText(response);
    }

    private async Task<string> GenerateStreamingAsync(string prompt, Action<string>? onChunk)
    {
        var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };

        if (onChunk is null)
        {
            var response = await PostAsync(body, stream: false);
            return ExtractText(response);
        }

        var json = JsonSerializer.Serialize(body);
        var url = $"{_baseUrl}:streamGenerateContent?alt=sse&key={apiKey}";

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode is 429 or 503 && attempt < MaxRetries - 1)
                    {
                        var waitMs = BackoffMs[attempt];
                        if (resp.Headers.RetryAfter?.Delta.HasValue == true)
                            waitMs = (int)resp.Headers.RetryAfter.Delta.Value.TotalMilliseconds;

                        AppLogger.Info($"Gemini stream rate limited (HTTP {resp.StatusCode}); retry in {waitMs}ms (attempt {attempt + 1}/{MaxRetries})");
                        await Task.Delay(waitMs);
                        continue;
                    }
                    resp.EnsureSuccessStatusCode();
                }

                var sb = new StringBuilder();
                await using var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null || !line.StartsWith("data: ")) continue;
                    var data = line["data: ".Length..];
                    if (data == "[DONE]") break;
                    try
                    {
                        var chunk = JsonNode.Parse(data);
                        var text  = chunk?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
                        if (!string.IsNullOrEmpty(text))
                        {
                            sb.Append(text);
                            onChunk(text);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Exception($"Streaming chunk parse (data={data[..Math.Min(data.Length, 100)]})", ex);
                    }
                }
                return sb.ToString().Trim();
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                AppLogger.Info($"Gemini stream request failed; retry in {BackoffMs[attempt]}ms: {ex.Message}");
                await Task.Delay(BackoffMs[attempt]);
                continue;
            }
        }

        throw new Exception("Gemini streaming request failed after max retries");
    }

    private async Task<string> PostAsync(object body, bool stream)
    {
        var endpoint = stream ? "streamGenerateContent" : "generateContent";
        var json     = JsonSerializer.Serialize(body);
        var url      = $"{_baseUrl}:{endpoint}?key={apiKey}";

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await _http.PostAsync(
                    url,
                    new StringContent(json, Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                // Handle rate limit / service unavailable
                if ((int)response.StatusCode is 429 or 503)
                {
                    if (attempt < MaxRetries - 1)
                    {
                        var waitMs = BackoffMs[attempt];
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                            waitMs = (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;

                        AppLogger.Info($"Gemini rate limited (HTTP {response.StatusCode}); retry in {waitMs}ms (attempt {attempt + 1}/{MaxRetries})");
                        await Task.Delay(waitMs);
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                AppLogger.Info($"Gemini request failed; retry in {BackoffMs[attempt]}ms: {ex.Message}");
                await Task.Delay(BackoffMs[attempt]);
                continue;
            }
        }

        throw new Exception("Gemini request failed after max retries");
    }

    private static string ExtractText(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        }
        catch (Exception ex)
        {
            AppLogger.Exception($"ExtractText failed (json={json[..Math.Min(json.Length, 200)]})", ex);
            return "";
        }
    }
}

