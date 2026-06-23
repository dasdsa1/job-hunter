namespace JobHunterApp.Models;

public class SearchConfig
{
    public string       JobTitle       { get; set; } = "";
    public string       Location       { get; set; } = "Remote";
    public string       Keywords       { get; set; } = "";
    public List<string> Sites          { get; set; } = ["linkedin", "indeed"];
    public int          MinScore       { get; set; } = 6;
    public int          MaxJobsPerSite { get; set; } = 20;
    public bool         EasyApplyOnly  { get; set; } = true;
}
