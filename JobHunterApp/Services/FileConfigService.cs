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
                // Decrypt secrets if they're encrypted
                if (!string.IsNullOrEmpty(config.ApiKey))
                    config.ApiKey = Decrypt(config.ApiKey);
                if (!string.IsNullOrEmpty(config.AdzunaAppKey))
                    config.AdzunaAppKey = Decrypt(config.AdzunaAppKey);
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
        // Encrypt secrets before saving
        var configToSave = new AppConfig
        {
            ApiKey = string.IsNullOrEmpty(config.ApiKey) ? "" : Encrypt(config.ApiKey),
            GeminiModel = config.GeminiModel,
            GeminiRpm = config.GeminiRpm,
            BrowserMode = config.BrowserMode,
            PreferredBrowser = config.PreferredBrowser,
            CdpPort = config.CdpPort,
            Cv = config.Cv,
            Letters = config.Letters,
            AdzunaAppId = config.AdzunaAppId,
            AdzunaAppKey = string.IsNullOrEmpty(config.AdzunaAppKey) ? "" : Encrypt(config.AdzunaAppKey),
            AdzunaCountry = config.AdzunaCountry
        };
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(configToSave, Opts));
    }

    private static string Encrypt(string plaintext)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex)
        {
            AppLogger.Exception("FileConfigService.Encrypt", ex);
            return plaintext; // Fallback to plaintext if encryption fails
        }
    }

    private static string Decrypt(string encrypted)
    {
        try
        {
            // Try to decrypt (assumes it's base64-encoded encrypted data)
            var data = Convert.FromBase64String(encrypted);
            var plaintext = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            // If decryption fails, assume it's plaintext (legacy or corruption)
            return encrypted;
        }
    }
}
