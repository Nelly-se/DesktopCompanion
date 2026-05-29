// =============================================================================
// ChatMessage.cs — EF 实体：聊天消息（表 ChatMessages）
// =============================================================================
// 数据结构：按 SpiritId 分「会话线程」；Sender 为 "user" 或 "spirit" 等字符串标识
// C# 语法：
//   - string? SpiritId：可空，历史数据或未归类消息可能无精灵 ID
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>单条聊天消息。</summary>
public class ChatMessage
{
    public int Id { get; set; }

    /// <summary>所属用户档案；null 表示历史演示数据。</summary>
    public int? UserProfileId { get; set; }

    /// <summary>发送方，如 "user"、"spirit"。</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>所属精灵会话 ID；null 表示未按精灵分线程。</summary>
    public string? SpiritId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
