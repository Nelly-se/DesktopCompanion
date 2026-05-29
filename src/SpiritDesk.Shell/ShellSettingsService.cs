// =============================================================================
// ShellSettingsService.cs — 读写 shell-settings.json
// =============================================================================
// 数据结构：ShellSettings 对象 ↔ JSON 文本（camelCase 命名）
// C# 语法：
//   - static readonly JsonSerializerOptions：序列化配置只创建一次
//   - ?? new ShellSettings()：反序列化 null 时回退默认对象
//   - Environment.SpecialFolder.ApplicationData：跨用户 AppData 路径
// =============================================================================

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
