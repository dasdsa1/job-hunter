using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class LlmServiceFactory
{
    /// <summary>All providers in fixed fallback priority (primary first, rest in this order).</summary>
    private static readonly LlmProvider[] AllProviders = [LlmProvider.Gemini, LlmProvider.Groq, LlmProvider.OpenRouter];

    /// <summary>Builds the primary provider wrapped with automatic fallback to any other
    /// provider that has an API key configured.</summary>
    public static ILlmService Create(AppConfig cfg)
    {
        var order = new[] { cfg.Provider }.Concat(AllProviders.Where(p => p != cfg.Provider));
        var chain = order
            .Where(p => !string.IsNullOrWhiteSpace(ApiKey(cfg, p)))
            .Select(p => (provider: p, service: CreateSingle(cfg, p)))
            .ToList();

        if (chain.Count == 0)
            throw new InvalidOperationException("No LLM provider has an API key configured.");

        return chain.Count == 1 ? chain[0].service : new FallbackLlmService(chain);
    }

    public static ILlmService CreateSingle(AppConfig cfg, LlmProvider provider)
    {
        var rateLimiter = new RateLimiter(Rpm(cfg, provider));
        return provider switch
        {
            LlmProvider.Groq       => new OpenAiCompatibleService("https://api.groq.com/openai/v1", cfg.GroqApiKey, cfg.GroqModel, rateLimiter),
            LlmProvider.OpenRouter => new OpenAiCompatibleService("https://openrouter.ai/api/v1", cfg.OpenRouterApiKey, cfg.OpenRouterModel, rateLimiter),
            _                      => new GeminiService(cfg.ApiKey, cfg.GeminiModel, rateLimiter)
        };
    }

    public static string ApiKey(AppConfig cfg, LlmProvider provider) => provider switch
    {
        LlmProvider.Groq       => cfg.GroqApiKey,
        LlmProvider.OpenRouter => cfg.OpenRouterApiKey,
        _                      => cfg.ApiKey
    };

    public static int Rpm(AppConfig cfg, LlmProvider provider) => provider switch
    {
        LlmProvider.Groq       => cfg.GroqRpm,
        LlmProvider.OpenRouter => cfg.OpenRouterRpm,
        _                      => cfg.GeminiRpm
    };

    public static string DefaultModel(LlmProvider p) => p switch
    {
        LlmProvider.Groq       => "llama-3.3-70b-versatile",
        LlmProvider.OpenRouter => "meta-llama/llama-3.3-70b-instruct:free",
        _                      => "gemini-flash-lite-latest"
    };
}
