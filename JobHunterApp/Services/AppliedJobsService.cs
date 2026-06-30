using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public class AppliedJob
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime AppliedAt { get; set; }
}

public class AppliedJobsStore
{
    public List<AppliedJob> Jobs { get; set; } = [];
}

public static class AppliedJobsService
{
    public static AppliedJobsStore Load()
    {
        try
        {
            if (!File.Exists(AppPaths.AppliedJobsFile))
            {
                AppLogger.Info("AppliedJobs: no file, starting fresh");
                return new AppliedJobsStore();
            }
            var json = File.ReadAllText(AppPaths.AppliedJobsFile);
            var result = JsonSerializer.Deserialize<AppliedJobsStore>(json) ?? new AppliedJobsStore();
            AppLogger.Info($"AppliedJobs: loaded {result.Jobs.Count} job(s)");
            return result;
        }
        catch (Exception ex)
        {
            AppLogger.Exception("AppliedJobsService.Load", ex);
            return new AppliedJobsStore();
        }
    }

    public static void Save(AppliedJobsStore store)
    {
        try
        {
            var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.AppliedJobsFile, json);
            AppLogger.Info($"AppliedJobs: saved {store.Jobs.Count} job(s)");
        }
        catch (Exception ex)
        {
            AppLogger.Exception("AppliedJobsService.Save", ex);
        }
    }

    public static bool IsApplied(string jobId, AppliedJobsStore store)
    {
        return store.Jobs.Any(j => j.Id == jobId);
    }

    public static void MarkApplied(string jobId, string title, string company, string source, AppliedJobsStore store)
    {
        if (!IsApplied(jobId, store))
        {
            store.Jobs.Add(new AppliedJob
            {
                Id = jobId,
                Title = title,
                Company = company,
                Source = source,
                AppliedAt = DateTime.Now
            });
        }
    }
}
