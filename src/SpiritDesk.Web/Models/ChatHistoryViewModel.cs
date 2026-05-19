using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

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
