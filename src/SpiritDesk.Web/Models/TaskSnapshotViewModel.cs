// =============================================================================
// TaskSnapshotViewModel.cs — 首页任务区「分组快照」DTO
// =============================================================================
// 数据结构：四个 List&lt;TaskItem&gt; 子集，由 SpiritDeskService 用 LINQ Where/Take 从全量任务筛选
// C# 语法：
//   - 表达式体属性 PendingCount => PendingTasks.Count：只读计算属性，不存字段
// =============================================================================

using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

/// <summary>待办/已完成/今日/临期 四组列表 + 计数。</summary>
public class TaskSnapshotViewModel
{
    public required List<TaskItem> PendingTasks { get; init; }
    public required List<TaskItem> CompletedTasks { get; init; }
    public required List<TaskItem> TodayTasks { get; init; }
    public required List<TaskItem> DueReminders { get; init; }

    /// <summary>未完成任务数（LINQ Count 的封装，供 cshtml 直接显示）。</summary>
    public int PendingCount => PendingTasks.Count;

    public int CompletedCount => CompletedTasks.Count;
}
