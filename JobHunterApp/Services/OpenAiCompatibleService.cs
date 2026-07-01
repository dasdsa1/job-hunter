using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JobHunterApp.Services;

/// <summary>Talks to any OpenAI-compatible chat completions endpoint (Groq, OpenRouter, ...).</summary>
public class OpenAiCompatibleService(string baseUrl, string apiKey, string model, RateLimiter rateLimiter)
    : LlmServiceBase(rateLimiter)
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };
    private const int MaxRetries = 4;
    private static readonly int[] BackoffMs = { 2000, 4000, 8000, 16000 };

    protected override async Task<string> GenerateJsonAsync(string prompt, object? responseSchema = null)
    {
        var body = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            response_format = new { type = "json_object" }
        };
        var response = await PostAsync(body);
        return ExtractText(response);
    }

    protected override async Task<string> GenerateTextAsync(string prompt)
    {
        var body = new { model, messages = new[] { new { role = "user", content = prompt } } };
        var response = await PostAsync(body);
        return ExtractText(response);
    }

    protected override async Task<string> GenerateStreamingAsync(string prompt, Action<string>? onChunk)
    {
        if (onChunk is null)
            return await GenerateTextAsync(prompt);

        var body = new { model, messages = new[] { new { role = "user", content = prompt } }, stream = true };
        var json = JsonSerializer.Serialize(body);
        var url  = $"{baseUrl}/chat/completions";

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var resp = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode is 429 or 503 && attempt < MaxRetries - 1)
                    {
                        var waitMs = BackoffMs[attempt];
                        if (resp.Headers.RetryAfter?.Delta.HasValue == true)
                            waitMs = (int)resp.Headers.RetryAfter.Delta.Value.TotalMilliseconds;

                        AppLogger.Info($"LLM stream rate limited (HTTP {resp.StatusCode}); retry in {waitMs}ms (attempt {attempt + 1}/{MaxRetries})");
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
                        var text  = chunk?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>() ?? "";
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
                AppLogger.Info($"LLM stream request failed; retry in {BackoffMs[attempt]}ms: {ex.Message}");
                await Task.Delay(BackoffMs[attempt]);
                continue;
            }
        }

        throw new Exception("LLM streaming request failed after max retries");
    }

    private async Task<string> PostAsync(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var url  = $"{baseUrl}/chat/completions";

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode is 429 or 503)
                {
                    if (attempt < MaxRetries - 1)
                    {
                        var waitMs = BackoffMs[attempt];
                        if (response.Headers.RetryAfter?.Delta.HasValue == true)
                            waitMs = (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;

                        AppLogger.Info($"LLM rate limited (HTTP {response.StatusCode}); retry in {waitMs}ms (attempt {attempt + 1}/{MaxRetries})");
                        await Task.Delay(waitMs);
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                AppLogger.Info($"LLM request failed; retry in {BackoffMs[attempt]}ms: {ex.Message}");
                await Task.Delay(BackoffMs[attempt]);
                continue;
            }
        }

        throw new Exception("LLM request failed after max retries");
    }

    private static string ExtractText(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? "";
        }
        catch (Exception ex)
        {
            AppLogger.Exception($"ExtractText failed (json={json[..Math.Min(json.Length, 200)]})", ex);
            return "";
        }
    }
}
