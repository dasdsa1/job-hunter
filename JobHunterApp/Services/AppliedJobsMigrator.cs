using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>
/// One-shot import of applied_jobs.json → SQLite. Idempotent: re-running
/// doesn't duplicate rows (matched by clientRef = jobId).
/// Run once on upgrade, then JSON write can stop.
/// </summary>
public static class AppliedJobsMigrator
{
    public static void MigrateIfNeeded(IDeviceStore store)
    {
        var jsonPath = AppPaths.AppliedJobsFile;
        if (!File.Exists(jsonPath))
        {
            AppLogger.Info("AppliedJobsMigrator: no JSON file, skipping");
            return;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<AppliedJobsStore>(json);
            if (data is null || data.Jobs.Count == 0)
            {
                AppLogger.Info("AppliedJobsMigrator: JSON file is empty, skipping");
                return;
            }

            foreach (var job in data.Jobs)
            {
                // clientRef = jobId (stable idempotency key)
                var payload = JsonSerializer.Serialize(new { job.Id, job.Title, job.Company, job.Source });
                store.InsertApplication(
                    id: Guid.NewGuid().ToString(),
                    clientRef: job.Id,
                    jobPostingId: job.Id,
                    status: "applied",
                    payloadJson: payload,
                    appliedAt: job.AppliedAt
                );
            }

            AppLogger.Info($"AppliedJobsMigrator: migrated {data.Jobs.Count} job(s) to SQLite");
        }
        catch (Exception ex)
        {
            AppLogger.Exception("AppliedJobsMigrator", ex);
        }
    }
}
