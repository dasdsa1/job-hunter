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
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(AppPaths.ConfigFile)) ?? new();
        }
        catch { }
        return new();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.ConfigFile)!);
        File.WriteAllText(AppPaths.ConfigFile, JsonSerializer.Serialize(config, Opts));
    }
}
