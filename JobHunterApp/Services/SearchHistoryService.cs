using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public class SearchHistory
{
    public List<string> JobTitles { get; set; } = [];
    public List<string> Locations { get; set; } = [];
    public List<string> Keywords  { get; set; } = [];
}

public static class SearchHistoryService
{
    private const int MaxPerField = 20;

    public static SearchHistory Load()
    {
        try
        {
            if (!File.Exists(AppPaths.SearchHistory)) return new SearchHistory();
            var json = File.ReadAllText(AppPaths.SearchHistory);
            return JsonSerializer.Deserialize<SearchHistory>(json) ?? new SearchHistory();
        }
        catch { return new SearchHistory(); }
    }

    public static void Save(SearchHistory history) =>
        File.WriteAllText(AppPaths.SearchHistory,
            JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));

    public static void AddEntry(List<string> list, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        list.Remove(value);           // deduplicate
        list.Insert(0, value);        // most-recent first
        if (list.Count > MaxPerField)
            list.RemoveRange(MaxPerField, list.Count - MaxPerField);
    }
}
