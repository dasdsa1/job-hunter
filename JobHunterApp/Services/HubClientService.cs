using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobHunterApp.Models;
using PlatformSchemas;

namespace JobHunterApp.Services;

/// <summary>POSTs a CBIF Curriculum to marketplace-hub (platform-schemas API contract:
/// POST /v1/curricula, Bearer token, X-CBIF-Version). Opt-in — a job-hunter install with no
/// HubBaseUrl configured runs standalone and never calls this.</summary>
public class HubClientService(HttpClient http)
{
    public HubClientService() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

    public async Task<bool> PostCurriculumAsync(AppConfig config, Curriculum curriculum)
    {
        if (string.IsNullOrWhiteSpace(config.HubBaseUrl)) return false;

        try
        {
            var url = $"{config.HubBaseUrl.TrimEnd('/')}/v1/curricula";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(curriculum)
            };
            request.Headers.TryAddWithoutValidation("X-CBIF-Version", "1");
            if (!string.IsNullOrWhiteSpace(config.HubToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.HubToken);

            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                AppLogger.Warn($"Hub curriculum POST failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Exception("HubClientService.PostCurriculumAsync", ex);
            return false;
        }
    }
}
