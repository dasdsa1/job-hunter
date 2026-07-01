namespace JobHunterApp.Models;

public enum BrowserMode { Managed, ConnectToBrowser }
public enum PreferredBrowser { Chrome, Edge, Firefox }
public enum LlmProvider { Gemini, Groq, OpenRouter }

public class AppConfig
{
    // Primary provider — tried first. On failure (rate-limited / erroring after retries),
    // falls back through the other providers below that have an API key configured,
    // in the fixed order Gemini -> Groq -> OpenRouter.
    public LlmProvider Provider    { get; set; } = LlmProvider.Gemini;

    public string ApiKey      { get; set; } = "";   // Gemini key (kept name for config.json back-compat)
    public string GeminiModel { get; set; } = "gemini-flash-lite-latest";
    public int    GeminiRpm   { get; set; } = 15;

    public string GroqApiKey  { get; set; } = "";
    public string GroqModel   { get; set; } = "llama-3.3-70b-versatile";
    public int    GroqRpm     { get; set; } = 30;

    public string OpenRouterApiKey { get; set; } = "";
    public string OpenRouterModel  { get; set; } = "meta-llama/llama-3.3-70b-instruct:free";
    public int    OpenRouterRpm    { get; set; } = 20;

    public BrowserMode      BrowserMode      { get; set; } = BrowserMode.Managed;
    public PreferredBrowser PreferredBrowser { get; set; } = PreferredBrowser.Chrome;
    public int              CdpPort          { get; set; } = 9222;
    public FileEntry?  Cv          { get; set; }
    public List<FileEntry> Letters  { get; set; } = [];

    // Adzuna API (free, requires signup at developer.adzuna.com). Other API
    // sources (Remotive, RemoteOK, Arbeitnow) need no credentials.
    public string AdzunaAppId   { get; set; } = "";
    public string AdzunaAppKey  { get; set; } = "";
    public string AdzunaCountry { get; set; } = "us";   // gb, us, de, fr, ...
}
