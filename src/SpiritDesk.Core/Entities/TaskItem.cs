// =============================================================================
// TaskItem.cs — EF 实体：待办任务（表 Tasks，EF 默认复数表名 Tasks）
// =============================================================================
// 数据结构：class；列表页用 List&lt;TaskItem&gt; 承载多条记录
// C# 语法：
//   - string? Description：可空引用类型，无描述时存 NULL
//   - DateTime? DueAt / CompletedAt：可选时间点
//   - bool IsCompleted：SQLite 存 0/1
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>用户任务项。</summary>
public class TaskItem
{
    public int Id { get; set; }

    /// <summary>所属用户档案；null 表示历史演示数据。</summary>
    public int? UserProfileId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>任务详情；null 表示无补充说明。</summary>
    public string? Description { get; set; }

    /// <summary>截止时间；null 表示无截止日期。</summary>
    public DateTime? DueAt { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>标记完成的时间；未完成时为 null。</summary>
    public DateTime? CompletedAt { get; set; }
}
