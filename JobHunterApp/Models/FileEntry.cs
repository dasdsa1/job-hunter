namespace JobHunterApp.Models;

public class FileEntry
{
    public string Key   { get; set; } = "";
    public string Label { get; set; } = "";
    public string Path  { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSelected { get; set; }
}
