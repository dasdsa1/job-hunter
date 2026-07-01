using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class LlmServiceFactory
{
    public static ILlmService Create(AppConfig cfg, RateLimiter rateLimiter) => cfg.Provider switch
    {
        LlmProvider.Groq       => new OpenAiCompatibleService("https://api.groq.com/openai/v1", cfg.ApiKey, cfg.GeminiModel, rateLimiter),
        LlmProvider.OpenRouter => new OpenAiCompatibleService("https://openrouter.ai/api/v1", cfg.ApiKey, cfg.GeminiModel, rateLimiter),
        _                      => new GeminiService(cfg.ApiKey, cfg.GeminiModel, rateLimiter)
    };

    public static string DefaultModel(LlmProvider p) => p switch
    {
        LlmProvider.Groq       => "llama-3.3-70b-versatile",
        LlmProvider.OpenRouter => "meta-llama/llama-3.3-70b-instruct:free",
        _                      => "gemini-flash-lite-latest"
    };
}
