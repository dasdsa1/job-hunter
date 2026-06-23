namespace JobHunterApp.Models;

public class JobMatch
{
    public JobListing   Job               { get; set; } = new();
    public MatchResult  Match             { get; set; } = new();
    public string?      CoverLetter       { get; set; }
    public bool         Applied           { get; set; }
    public string?      ApplicationStatus { get; set; }  // "submitted" | "pending" | "failed"
    public bool         IsSelected        { get; set; }  // for UI checkbox
}
