namespace SpiritDesk.Core.Entities;

public class DailyActionLog
{
    public int Id { get; set; }
    public DateOnly ActionDate { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int Count { get; set; }
}
