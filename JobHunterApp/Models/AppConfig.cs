namespace JobHunterApp.Models;

public enum BrowserMode { Managed, ConnectToBrowser }
public enum PreferredBrowser { Chrome, Edge, Firefox }
public enum LlmProvider { Gemini, Groq, OpenRouter }

public class AppConfig
{
    public LlmProvider Provider    { get; set; } = LlmProvider.Gemini;
    public string      ApiKey      { get; set; } = "";
    public string      GeminiModel { get; set; } = "gemini-flash-lite-latest";
    public int         GeminiRpm   { get; set; } = 15;
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
