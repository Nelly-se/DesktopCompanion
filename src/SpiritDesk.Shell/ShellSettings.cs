// =============================================================================
// ShellSettings.cs — 浮球窗口状态 DTO（JSON 持久化，非 EF）
// =============================================================================
// 存储路径：%AppData%\SpiritDesk\shell-settings.json（见 ShellSettingsService）
// 数据结构：标量 double/bool/string，无集合
// C# 语法：
//   - sealed class：不可继承，适合单一配置 DTO
//   - { get; set; }：System.Text.Json 反序列化需要 setter
//   - 属性初始化器 = true / = 320：JSON 缺失字段时的默认值
// =============================================================================

namespace SpiritDesk.Shell;

/// <summary>浮球位置、展开、尺寸、主题。</summary>
public sealed class ShellSettings
{
    public double CompanionBubbleLeft { get; set; }
    public double CompanionBubbleTop { get; set; }

    public bool CompanionBubbleExpanded { get; set; }

    public bool CompanionBubbleTopmost { get; set; } = true;
    public double CompanionPanelWidth { get; set; } = 320;
    public double CompanionPanelHeight { get; set; } = 220;

    /// <summary>主题键：mint / warm。</summary>
    public string CompanionTheme { get; set; } = "mint";
}
