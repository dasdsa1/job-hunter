using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class FileConfigService
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(AppPaths.ConfigFile))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile)) ?? new();
                // Decrypt API key if it's encrypted
                if (!string.IsNullOrEmpty(config.ApiKey))
                {
                    config.ApiKey = DecryptApiKey(config.ApiKey);
                }
                return config;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Exception("FileConfigService.Load", ex);
        }
        return new();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ConfigFile)!);
        // Encrypt API key before saving
        var configToSave = new AppConfig
        {
            ApiKey = string.IsNullOrEmpty(config.ApiKey) ? "" : EncryptApiKey(config.ApiKey),
            GeminiModel = config.GeminiModel,
            GeminiRpm = config.GeminiRpm,
            BrowserMode = config.BrowserMode,
            PreferredBrowser = config.PreferredBrowser,
            CdpPort = config.CdpPort,
            Cv = config.Cv,
            Letters = config.Letters
        };
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(configToSave, Opts));
    }

    private static string EncryptApiKey(string apiKey)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(apiKey);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex)
        {
            AppLogger.Exception("FileConfigService.EncryptApiKey", ex);
            return apiKey; // Fallback to plaintext if encryption fails
        }
    }

    private static string DecryptApiKey(string encryptedApiKey)
    {
        try
        {
            // Try to decrypt (assumes it's base64-encoded encrypted data)
            var encrypted = Convert.FromBase64String(encryptedApiKey);
            var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            // If decryption fails, assume it's plaintext (legacy or corruption)
            return encryptedApiKey;
        }
    }
}
