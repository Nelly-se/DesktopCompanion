namespace SpiritDesk.Core.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string CurrentSpiritId { get; set; } = string.Empty;
    public int Mood { get; set; } = 72;
    public int Affinity { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int Coins { get; set; } = 20;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
