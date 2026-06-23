namespace JobHunterApp.Models;

public class MatchResult
{
    public int          Score   { get; set; }
    public string       Summary { get; set; } = "";
    public List<string> Reasons { get; set; } = [];
}
