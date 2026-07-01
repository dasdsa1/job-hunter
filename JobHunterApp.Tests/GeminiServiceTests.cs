using JobHunterApp.Models;
using JobHunterApp.Services;

namespace JobHunterApp.Tests;

public class GeminiServiceTests
{
    /// <summary>Test batch splitting: verify correct number of batches for various job counts.</summary>
    [Theory]
    [InlineData(5, 1)]    // 1-20 jobs → 1 batch
    [InlineData(20, 1)]
    [InlineData(21, 2)]   // 21-40 jobs → 2 batches
    [InlineData(50, 3)]   // 50 jobs → 3 batches (20 + 20 + 10)
    [InlineData(60, 3)]   // 60 jobs → 3 batches (20 + 20 + 20)
    [InlineData(61, 4)]   // 61 jobs → 4 batches
    public async Task MatchJobsAsync_SplitsBatchesCorrectly(int jobCount, int expectedBatchCount)
    {
        var rateLimiter = new RateLimiter(60);
        var gemini = new BatchCountingGeminiService(rateLimiter);

        var jobs = Enumerable.Range(0, jobCount)
            .Select(i => new JobListing
            {
                Id = $"job-{i}",
                Title = $"Job {i}",
                Company = "TestCo",
                Location = "Remote",
                Description = "Test job description",
                Salary = null,
                Url = "https://example.com",
                Source = "TestSource",
                PostedDate = "2026-07-01"
            })
            .ToList();

        var profile = "Test profile";
        var result = await gemini.MatchJobsAsync(jobs, profile);

        // Verify correct number of batches were processed
        Assert.Equal(expectedBatchCount, gemini.BatchesProcessed);
    }

    /// <summary>Test JSON parsing: valid Gemini response format should extract text correctly.</summary>
    [Fact]
    public void ExtractText_ParsesValidJsonCorrectly()
    {
        var json = @"{""candidates"":[{""content"":{""parts"":[{""text"":""This is the extracted text""}]}}]}";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("This is the extracted text", result);
    }

    /// <summary>Test JSON parsing: malformed JSON should return empty string, not throw.</summary>
    [Fact]
    public void ExtractText_MalformedJson_ReturnsEmpty()
    {
        var json = "{\"invalid\": \"json\"";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("", result);
    }

    /// <summary>Test JSON parsing: missing text field should return empty string.</summary>
    [Fact]
    public void ExtractText_MissingTextField_ReturnsEmpty()
    {
        var json = @"{""candidates"":[{""content"":{""parts"":[{}]}}]}";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("", result);
    }

    /// <summary>Test JSON parsing: empty text should return empty string, not null.</summary>
    [Fact]
    public void ExtractText_EmptyText_ReturnsEmptyString()
    {
        var json = @"{""candidates"":[{""content"":{""parts"":[{""text"":""""}]}}]}";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("", result);
    }

    /// <summary>Test JSON parsing: missing candidates array should return empty.</summary>
    [Fact]
    public void ExtractText_MissingCandidates_ReturnsEmpty()
    {
        var json = @"{""other"": ""field""}";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("", result);
    }

    /// <summary>Test JSON parsing: null candidates should return empty.</summary>
    [Fact]
    public void ExtractText_NullCandidates_ReturnsEmpty()
    {
        var json = @"{""candidates"": null}";

        var method = typeof(GeminiService).GetMethod(
            "ExtractText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null
        );

        Assert.NotNull(method);
        var result = (string?)method!.Invoke(null, new object[] { json });
        Assert.Equal("", result);
    }

    /// <summary>Mock service that tracks batch count without making HTTP calls.</summary>
    private sealed class BatchCountingGeminiService : LlmServiceBase
    {
        public int BatchesProcessed { get; set; }

        public BatchCountingGeminiService(RateLimiter rateLimiter) : base(rateLimiter) { }

        protected override Task<string> GenerateJsonAsync(string prompt, object? responseSchema = null)
        {
            BatchesProcessed++;
            // Return empty array to avoid parsing errors
            // We're only testing batch splitting here, not the parsing
            return Task.FromResult("[]");
        }

        protected override Task<string> GenerateTextAsync(string prompt)
        {
            return Task.FromResult("Test profile extracted");
        }

        protected override Task<string> GenerateStreamingAsync(string prompt, Action<string>? onChunk)
        {
            return Task.FromResult("Streamed response");
        }
    }
}
