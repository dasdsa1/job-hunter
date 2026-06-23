using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public class SearchState
{
    public string JobTitle              { get; set; } = "";
    public string Location              { get; set; } = "Remote";
    public string Keywords              { get; set; } = "";
    public bool   UseLinkedIn           { get; set; } = true;
    public bool   UseIndeed             { get; set; } = true;
    public bool   LinkedInEasyApplyOnly { get; set; } = true;
    public bool   IndeedApplyOnly       { get; set; } = false;
    public int    MinScore              { get; set; } = 6;
    public int    MaxJobsPerSite        { get; set; } = 20;
}

public class SearchHistory
{
    public List<string> JobTitles { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public List<string> Keywords  { get; set; } = [];
    public SearchState  LastState { get; set; } = new();
}

public static class SearchHistoryService
{
    private const int MaxPerField = 20;

    public static SearchHistory Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SearchHistory))
            {
                AppLogger.Info($"SearchHistory: no file at {AppPaths.SearchHistory}, starting fresh");
                return new SearchHistory();
            }
            var json = File.ReadAllText(AppPaths.SearchHistory);
            var result = JsonSerializer.Deserialize<SearchHistory>(json) ?? new SearchHistory();
            result.LastState ??= new SearchState();
            AppLogger.Info($"SearchHistory: loaded — titles:{result.JobTitles.Count}, lastJobTitle:'{result.LastState.JobTitle}'");
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Exception("SearchHistoryService.Load", ex);
            return new SearchHistory();
        }
    }

    public static void Save(SearchHistory history)
    {
        try
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.SearchHistory, json);
            AppLogger.Info($"SearchHistory: saved — lastJobTitle:'{history.LastState.JobTitle}'");
        }
        catch (Exception ex)
        {
            AppLogger.Exception("SearchHistoryService.Save", ex);
        }
    }

    public static void AddEntry(List<string> list, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        list.Remove(value);
        list.Insert(0, value);
        if (list.Count > MaxPerField)
            list.RemoveRange(MaxPerField, list.Count - MaxPerField);
    }
}
