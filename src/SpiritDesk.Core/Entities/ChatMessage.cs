namespace SpiritDesk.Core.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
