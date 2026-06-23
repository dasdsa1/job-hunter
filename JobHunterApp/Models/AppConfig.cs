namespace JobHunterApp.Models;

public class AppConfig
{
    public string    ApiKey       { get; set; } = "";
    public string    GeminiModel  { get; set; } = "gemini-2.0-flash-lite";
    public int       GeminiRpm    { get; set; } = 15;
    public FileEntry? Cv          { get; set; }
    public List<FileEntry> Letters { get; set; } = [];
}
