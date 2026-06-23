using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public class GeminiService(string apiKey, string model, RateLimiter rateLimiter)
{
    private readonly HttpClient _http = new();
    private readonly string _baseUrl =
        $"https://generativelanguage.googleapis.com/v1beta/models/{model}";

    public async Task<Dictionary<string, MatchResult>> MatchJobsAsync(
        IEnumerable<JobListing> jobs, string resume)
    {
        var snippets = jobs.Select(j => new
        {
            id          = j.Id,
            title       = j.Title,
            company     = j.Company,
            location    = j.Location,
            description = j.Description[..Math.Min(j.Description.Length, 1_000)]
        });

        var prompt = $$"""
            You are a professional job application assistant.
            Evaluate each job listing against the candidate's resume and assign a match score.

            Scoring guide:
              9-10  â€” Near-perfect fit: skills, seniority, and domain all align
              7-8   â€” Strong match: most requirements met, minor gaps
              5-6   â€” Moderate: roughly half the requirements match
              3-4   â€” Weak: significant skill or experience gaps
              1-2   â€” Poor fit

            Resume:
            ---
            {{resume}}
            ---

            Jobs to evaluate (JSON):
            {{JsonSerializer.Serialize(snippets)}}

            Return a JSON array â€” one object per job:
            [
              {
                "id": "<job id>",
                "score": <1-10 integer>,
                "summary": "<one sentence explaining the score>",
                "reasons": ["<reason 1>", "<reason 2>", "<reason 3>"]
              }
            ]
            """;

        await rateLimiter.ThrottleAsync();
        var raw = await GenerateJsonAsync(prompt);

        var result = new Dictionary<string, MatchResult>();
        try
        {
            var arr = JsonNode.Parse(raw)?.AsArray();
            if (arr is null) return result;
            foreach (var item in arr)
            {
                var id      = item?["id"]?.GetValue<string>() ?? "";
                var score   = item?["score"]?.GetValue<int>() ?? 0;
                var summary = item?["summary"]?.GetValue<string>() ?? "";
                var reasons = item?["reasons"]?.AsArray()
                    .Select(r => r?.GetValue<string>() ?? "").ToList() ?? [];
                result[id] = new MatchResult
                {
                    Score   = Math.Clamp(score, 1, 10),
                    Summary = summary,
                    Reasons = reasons
                };
            }
        }
        catch { }
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
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_baseUrl}:streamGenerateContent?alt=sse&key={apiKey}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

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
            catch { }
        }
        return sb.ToString().Trim();
    }

    private async Task<string> PostAsync(object body, bool stream)
    {
        var endpoint = stream ? "streamGenerateContent" : "generateContent";
        var json     = JsonSerializer.Serialize(body);
        var response = await _http.PostAsync(
            $"{_baseUrl}:{endpoint}?key={apiKey}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static string ExtractText(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>() ?? "";
        }
        catch { return ""; }
    }
}

