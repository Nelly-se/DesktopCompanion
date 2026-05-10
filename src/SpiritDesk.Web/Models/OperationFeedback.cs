namespace SpiritDesk.Web.Models;

public class OperationFeedback
{
    public bool Succeeded { get; init; }
    public required string Message { get; init; }
    public int MoodDelta { get; init; }
    public int AffinityDelta { get; init; }
    public int CoinsDelta { get; init; }
    public int LevelDelta { get; init; }

    public string ToNoticeMessage()
    {
        var deltas = new List<string>();

        if (MoodDelta != 0)
        {
            deltas.Add($"心情 {(MoodDelta > 0 ? "+" : string.Empty)}{MoodDelta}");
        }

        if (AffinityDelta != 0)
        {
            deltas.Add($"亲密度 {(AffinityDelta > 0 ? "+" : string.Empty)}{AffinityDelta}");
        }

        if (CoinsDelta != 0)
        {
            deltas.Add($"金币 {(CoinsDelta > 0 ? "+" : string.Empty)}{CoinsDelta}");
        }

        if (LevelDelta != 0)
        {
            deltas.Add($"等级 {(LevelDelta > 0 ? "+" : string.Empty)}{LevelDelta}");
        }

        return deltas.Count == 0
            ? Message
            : $"{Message} · {string.Join(" · ", deltas)}";
    }
}
