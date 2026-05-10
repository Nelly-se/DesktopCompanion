using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

public class SpiritDeskViewModel
{
    public required UserProfile Profile { get; init; }
    public required SpiritDefinition CurrentSpirit { get; init; }
    public required List<SpiritDefinition> Spirits { get; init; }
    public required TaskSnapshotViewModel TaskSnapshot { get; init; }
    public required List<TaskItem> Tasks { get; init; }
    public required List<ChatMessage> ChatMessages { get; init; }
    public required Dictionary<string, int> DailyCounts { get; init; }
    public required string Greeting { get; init; }
    public required string WelcomeBackMessage { get; init; }
    public required string HelperTip { get; init; }
    public string? ReturnNotice { get; init; }
}
