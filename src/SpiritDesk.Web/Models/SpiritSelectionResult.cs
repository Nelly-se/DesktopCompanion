// =============================================================================
// SpiritSelectionResult.cs — 选择/切换精灵操作返回 DTO
// =============================================================================
// 数据结构：bool Succeeded + 文案 + 精灵显示名；PageModel 写入 TempData/NoticeMessage
// C# 语法：init 与默认 string.Empty（Succeeded=false 时 Message 仍有默认值）
// =============================================================================

namespace SpiritDesk.Web.Models;

/// <summary>SpiritDeskService.SelectSpiritAsync 返回值。</summary>
public class SpiritSelectionResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SpiritName { get; init; } = string.Empty;
}
