using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

public class SettingsViewModel
{
    public required UserProfile Profile { get; init; }
    public required SpiritDefinition CurrentSpirit { get; init; }
    public required List<SpiritDefinition> Spirits { get; init; }
    public required bool CanSwitchSpirit { get; init; }
    public required string CooldownMessage { get; init; }
    public DateTime? NextSwitchAvailableAt { get; init; }
}
