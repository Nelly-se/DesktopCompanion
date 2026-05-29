// =============================================================================
// CompanionApiModels.cs — 浮球 API JSON 反序列化 DTO
// =============================================================================
// 数据结构：CompanionSelectBody 对应 POST /api/companion/select-spirit 请求体
// C# 语法：
//   - sealed class：禁止继承，适合单一用途 DTO
//   - { get; set; }：System.Text.Json 反序列化需要可写属性（非 init）
// JSON 示例：{ "spiritId": "light" }
// =============================================================================

namespace SpiritDesk.Web.Models;

/// <summary>切换精灵 API 请求体；spiritId 须为 SpiritIds 之一。</summary>
public sealed class CompanionSelectBody
{
    public string SpiritId { get; set; } = string.Empty;
}
