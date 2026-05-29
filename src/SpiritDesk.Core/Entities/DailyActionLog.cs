// =============================================================================
// DailyActionLog.cs — EF 实体：每日互动计数（表 DailyActionLogs）
// =============================================================================
// 数据结构：复合业务键 (ActionDate, ActionType) 逻辑上唯一；内存中常转为
//   Dictionary&lt;string, int&gt;（ActionType → Count）供首页展示
// C# 语法：
//   - DateOnly：仅日期（年-月-日），EF 在 SQLite 中通过值转换存 DateTime
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>某天某类互动（签到/投喂/猜拳等）的执行次数。</summary>
public class DailyActionLog
{
    public int Id { get; set; }

    /// <summary>所属用户档案；null 表示历史演示数据。</summary>
    public int? UserProfileId { get; set; }

    /// <summary>统计日期（不含时分秒）。</summary>
    public DateOnly ActionDate { get; set; }

    /// <summary>行为类型键，如 checkin、feed、game。</summary>
    public string ActionType { get; set; } = string.Empty;

    public int Count { get; set; }
}
