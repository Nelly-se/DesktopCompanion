namespace SpiritDesk.Core.Entities;

/// <summary>
/// 站点 Cookie 登录账号（与精灵档案 UserProfile 分离）。
/// </summary>
public class WebAccount
{
    public int Id { get; set; }

    /// <summary>已规范化的小写用户名，唯一。</summary>
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
