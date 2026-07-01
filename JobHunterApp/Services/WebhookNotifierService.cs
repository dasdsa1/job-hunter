using System.Net.Http.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>Posts a JSON summary to a user-configured webhook when a headless run finishes.
/// Payload includes a "text" field so it renders directly in Slack/Discord incoming
/// webhooks without extra formatting; other endpoints can read the structured fields.</summary>
public static class WebhookNotifierService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task NotifyRunFinishedAsync(AppConfig config, int totalJobs, int matches, string reportPath)
    {
        if (string.IsNullOrWhiteSpace(config.NotificationWebhookUrl)) return;

        var payload = new
        {
            text      = $"Job Hunter run finished: {matches}/{totalJobs} jobs matched.",
            totalJobs,
            matches,
            reportPath
        };

        try
        {
            var response = await Http.PostAsJsonAsync(config.NotificationWebhookUrl, payload);
            if (!response.IsSuccessStatusCode)
                AppLogger.Warn($"Webhook notification failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            AppLogger.Exception("WebhookNotifierService.NotifyRunFinishedAsync", ex);
        }
    }
}
