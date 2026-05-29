// =============================================================================
// ChatHistoryViewModel.cs — 聊天页 Chat 展示 DTO
// =============================================================================
// 数据结构：
//   - Messages：全表最近消息（按 CreatedAt 排序后加载）
//   - CurrentConversationMessages：按 CurrentSpirit.Id 过滤的子集（同一会话线程）
//   - 统计字段 int：LINQ Count 结果，避免 cshtml 里写复杂表达式
// C# 语法：required + init，与 SpiritDeskViewModel 相同模式
// =============================================================================

using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

/// <summary>Chat.cshtml 的 @Model。</summary>
public class ChatHistoryViewModel
{
    public required UserProfile Profile { get; init; }
    public required SpiritDefinition CurrentSpirit { get; init; }
    public required List<SpiritDefinition> Spirits { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public required List<ChatMessage> CurrentConversationMessages { get; init; }
    public required int TotalMessageCount { get; init; }
    public required int ActiveThreadCount { get; init; }
    public required int UserMessageCount { get; init; }
    public required int SpiritMessageCount { get; init; }
}
