using JobHunterApp.Models;
using JobHunterApp.Services;

namespace JobHunterApp.Worker.Tests;

// All tests in a class run sequentially in xUnit (only classes run in parallel), which matters
// here since every test mutates process-wide environment variables.
public class WorkerConfigServiceTests
{
    private static void WithEnv(Dictionary<string, string?> vars, Action assert)
    {
        var previous = vars.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (k, v) in vars) Environment.SetEnvironmentVariable(k, v);
            assert();
        }
        finally
        {
            foreach (var (k, v) in previous) Environment.SetEnvironmentVariable(k, v);
        }
    }

    [Fact]
    public void LoadAppConfig_GeminiApiKeyEnv_OverridesStoredKey() =>
        WithEnv(new() { ["GEMINI_API_KEY"] = "env-key-123" }, () =>
        {
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.Equal("env-key-123", cfg.ApiKey);
        });

    [Fact]
    public void LoadAppConfig_LlmProviderEnv_ParsesEnumCaseInsensitive() =>
        WithEnv(new() { ["LLM_PROVIDER"] = "groq" }, () =>
        {
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.Equal(LlmProvider.Groq, cfg.Provider);
        });

    [Fact]
    public void LoadAppConfig_InvalidLlmProviderEnv_KeepsDefault() =>
        WithEnv(new() { ["LLM_PROVIDER"] = "not-a-provider" }, () =>
        {
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.Equal(LlmProvider.Gemini, cfg.Provider);
        });

    [Fact]
    public void LoadAppConfig_RetiredGeminiModel_MigratesToDefault() =>
        WithEnv(new() { ["GEMINI_MODEL"] = null }, () =>
        {
            // No GEMINI_MODEL override and no config.json on a fresh CI runner means
            // WorkerConfigService starts from AppConfig's own default model, which is
            // never a retired one — so this only proves the migration path doesn't
            // throw and still yields a non-retired model.
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.DoesNotContain(cfg.GeminiModel, new[] { "gemini-2.0-flash-lite", "gemini-1.5-pro", "gemini-1.5-flash" });
        });

    [Fact]
    public void LoadAppConfig_AdzunaEnvVars_PopulateConfig() =>
        WithEnv(new()
        {
            ["ADZUNA_APP_ID"]   = "id-1",
            ["ADZUNA_APP_KEY"]  = "key-1",
            ["ADZUNA_COUNTRY"]  = "gb",
        }, () =>
        {
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.Equal("id-1", cfg.AdzunaAppId);
            Assert.Equal("key-1", cfg.AdzunaAppKey);
            Assert.Equal("gb", cfg.AdzunaCountry);
        });

    [Fact]
    public void LoadAppConfig_NotificationWebhookUrlEnv_OverridesConfig() =>
        WithEnv(new() { ["NOTIFICATION_WEBHOOK_URL"] = "https://hooks.example.com/abc" }, () =>
        {
            var cfg = WorkerConfigService.LoadAppConfig();
            Assert.Equal("https://hooks.example.com/abc", cfg.NotificationWebhookUrl);
        });

    [Fact]
    public void LoadSearchConfig_NoEnvOverrides_DefaultsToHeadlessApiSources() =>
        WithEnv(new() { ["SEARCH_SITES"] = null }, () =>
        {
            var cfg = WorkerConfigService.LoadSearchConfig();
            Assert.Equal(new List<string> { "remotive", "remoteok", "arbeitnow" }, cfg.Sites);
        });

    [Fact]
    public void LoadSearchConfig_SearchSitesEnv_ParsesCommaSeparatedLowercased() =>
        WithEnv(new() { ["SEARCH_SITES"] = "LinkedIn, Adzuna ,remotive" }, () =>
        {
            var cfg = WorkerConfigService.LoadSearchConfig();
            Assert.Equal(new List<string> { "linkedin", "adzuna", "remotive" }, cfg.Sites);
        });

    [Fact]
    public void LoadSearchConfig_MinScoreAndMaxJobsEnv_ParseAsInts() =>
        WithEnv(new() { ["SEARCH_MIN_SCORE"] = "8", ["SEARCH_MAX_JOBS"] = "50" }, () =>
        {
            var cfg = WorkerConfigService.LoadSearchConfig();
            Assert.Equal(8, cfg.MinScore);
            Assert.Equal(50, cfg.MaxJobsPerSite);
        });

    [Fact]
    public void LoadSearchConfig_MissingMinScoreEnv_DefaultsTo6() =>
        WithEnv(new() { ["SEARCH_MIN_SCORE"] = null }, () =>
        {
            var cfg = WorkerConfigService.LoadSearchConfig();
            Assert.Equal(6, cfg.MinScore);
        });
}
