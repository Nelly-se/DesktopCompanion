namespace SpiritDesk.Shell;

public sealed class ShellSettings
{
    public double CompanionBubbleLeft { get; set; }
    public double CompanionBubbleTop { get; set; }
    public bool CompanionBubbleExpanded { get; set; }
    public bool CompanionBubbleTopmost { get; set; } = true;
    public double CompanionPanelWidth { get; set; } = 308;
    public double CompanionPanelHeight { get; set; } = 172;
    public string CompanionTheme { get; set; } = "mint";
}
