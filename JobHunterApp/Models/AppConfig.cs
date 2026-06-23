namespace JobHunterApp.Models;

public enum BrowserMode { Managed, ConnectToBrowser }
public enum PreferredBrowser { Chrome, Edge, Firefox }

public class AppConfig
{
    public string      ApiKey      { get; set; } = "";
    public string      GeminiModel { get; set; } = "gemini-2.0-flash-lite";
    public int         GeminiRpm   { get; set; } = 15;
    public BrowserMode      BrowserMode      { get; set; } = BrowserMode.ConnectToBrowser;
    public PreferredBrowser PreferredBrowser { get; set; } = PreferredBrowser.Chrome;
    public int              CdpPort          { get; set; } = 9222;
    public FileEntry?  Cv          { get; set; }
    public List<FileEntry> Letters  { get; set; } = [];
}
