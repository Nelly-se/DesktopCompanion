namespace SpiritDesk.Web.Models;

public class SpiritSelectionResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SpiritName { get; init; } = string.Empty;
}
