// =============================================================================
// WebAccount.cs — EF 实体：站点登录账号（表 WebAccounts）
// =============================================================================
// 数据结构：与 UserProfile 无 EF 外键关联（演示版单档案）；Username 唯一索引
// C# 语法：
//   - DateTimeOffset：带时区偏移的时间，注册时间用 UtcNow 写入
//   - PasswordHash：仅存哈希，明文密码从不入库（见 Register PageModel + IPasswordHasher）
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>Cookie 登录账号。</summary>
public class WebAccount
{
    public int Id { get; set; }

    /// <summary>规范化小写用户名，数据库唯一。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>ASP.NET Identity PasswordHasher 生成的哈希。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
