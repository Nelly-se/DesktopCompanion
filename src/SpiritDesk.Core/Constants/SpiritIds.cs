// =============================================================================
// SpiritIds.cs — 五精灵字符串主键常量（编译期常量，非数据库表）
// =============================================================================
// 数据结构：static class + public const string（无实例，全局共享只读字符串）
// 用途：与 SpiritDefinition.Id、UserProfile.CurrentSpiritId、ChatMessage.SpiritId 对齐，
//       避免代码里散落魔法字符串 "air"、"water"。
// C# 语法：
//   - static class：不能 new，只放静态成员
//   - const：编译期常量，引用处可能被内联，比 readonly 更严格
// =============================================================================

namespace SpiritDesk.Core.Constants;

/// <summary>精灵 ID 常量集合。</summary>
public static class SpiritIds
{
    /// <summary>风系 · 贴贴朵</summary>
    public const string Air = "air";

    /// <summary>光系 · 卷卷晴</summary>
    public const string Light = "light";

    /// <summary>营养系 · 新新星</summary>
    public const string Nutrition = "nutrition";

    /// <summary>土系 · 慢慢壤</summary>
    public const string Soil = "soil";

    /// <summary>水系 · 嘻嘻滴</summary>
    public const string Water = "water";
}
