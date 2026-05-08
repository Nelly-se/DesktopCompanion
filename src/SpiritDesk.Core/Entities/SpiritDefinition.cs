namespace SpiritDesk.Core.Entities;

public class SpiritDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mbti { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CoreRole { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DialogueExample { get; set; } = string.Empty;
    public string SpecialMechanism { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public int CheckInBonusMultiplier { get; set; } = 1;
    public int TaskAffinityBonus { get; set; }
    public int FeedMoodBonus { get; set; }
    public int GameCountBonus { get; set; }
    public int GameCoinBonus { get; set; }
    public int MoodDecayReduction { get; set; }
    public bool WelcomeBackCompensation { get; set; }
}
