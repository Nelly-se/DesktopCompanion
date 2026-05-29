// =============================================================================
// UserProfile.cs — EF 实体：玩家养成档案（表 UserProfiles）
// =============================================================================
// 数据结构：class POCO，属性 ↔ SQLite 列；int 主键自增，string 为文本列
// 与登录账号通过 AccountUsername 绑定：账号管认证，本表管游戏进度
// C# 语法：
//   - { get; set; }：自动属性，EF Core 映射列并可变更
//   - string = string.Empty：非空字符串默认值（C# 11+ 可空引用类型下避免 null）
//   - DateTime?：可空值类型，列允许 NULL（如 LastSpiritSwitchAt 未切换过）
//   - DateTime.Now：属性初始化器，new 对象时写入默认时间
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>玩家/演示档案。</summary>
public class UserProfile
{
    /// <summary>主键，数据库自增 INTEGER。</summary>
    public int Id { get; set; }

    /// <summary>显示昵称。</summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>绑定的登录用户名（规范化小写）；null 表示本地免登录/历史演示档案。</summary>
    public string? AccountUsername { get; set; }

    /// <summary>当前陪伴精灵 ID，取值见 <see cref="Constants.SpiritIds"/>。</summary>
    public string CurrentSpiritId { get; set; } = string.Empty;

    /// <summary>心情值 0–100。</summary>
    public int Mood { get; set; } = 72;

    /// <summary>与当前精灵的亲密度。</summary>
    public int Affinity { get; set; } = 0;

    public int Level { get; set; } = 1;

    public int Coins { get; set; } = 20;

    /// <summary>上次切换精灵时间；null 表示尚未切换过。</summary>
    public DateTime? LastSpiritSwitchAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
