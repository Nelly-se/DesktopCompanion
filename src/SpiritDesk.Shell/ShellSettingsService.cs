using System.IO;
using System.Diagnostics;
using System.Text.Json;

namespace SpiritDesk.Shell;

public sealed class ShellSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ShellSettings Load()
    {
        try
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path))
            {
                return new ShellSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ShellSettings>(json, JsonOptions) ?? new ShellSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpiritDesk.Shell] Failed to load settings: {ex.Message}");
            return new ShellSettings();
        }
    }

    public void Save(ShellSettings settings)
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(GetSettingsFilePath(), json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpiritDesk.Shell] Failed to save settings: {ex.Message}");
        }
    }

    public string GetSettingsFilePath()
    {
        return Path.Combine(GetSettingsDirectory(), "shell-settings.json");
    }

    private void EnsureDirectoryExists()
    {
        Directory.CreateDirectory(GetSettingsDirectory());
    }

    private static string GetSettingsDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpiritDesk");
    }
}
