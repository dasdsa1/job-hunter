namespace JobHunterApp.Models;

public class JobListing
{
    public string  Id          { get; set; } = "";
    public string  Title       { get; set; } = "";
    public string  Company     { get; set; } = "";
    public string  Location    { get; set; } = "";
    public string  Description { get; set; } = "";
    public string  Url         { get; set; } = "";
    public string  Source      { get; set; } = "";   // "linkedin" | "indeed"
    public bool    IsEasyApply { get; set; }
    public string? PostedDate  { get; set; }
    public string? Salary      { get; set; }
}
