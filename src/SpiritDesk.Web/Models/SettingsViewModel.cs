// =============================================================================
// SettingsViewModel.cs — 设置页 Settings 展示 DTO
// =============================================================================
// 数据结构：档案 + 精灵列表 + 切精灵 UI 状态（演示版 CanSwitchSpirit 恒为 true）
// C# 语法：DateTime? NextSwitchAvailableAt 可空，预留冷却结束时间展示
// =============================================================================

using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

/// <summary>Settings.cshtml 的 @Model。</summary>
public class SettingsViewModel
{
    public required UserProfile Profile { get; init; }
    public required SpiritDefinition CurrentSpirit { get; init; }
    public required List<SpiritDefinition> Spirits { get; init; }
    public required bool CanSwitchSpirit { get; init; }
    public required string CooldownMessage { get; init; }

    public DateTime? NextSwitchAvailableAt { get; init; }
}
