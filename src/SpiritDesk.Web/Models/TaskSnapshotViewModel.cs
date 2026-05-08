using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

public class TaskSnapshotViewModel
{
    public required List<TaskItem> PendingTasks { get; init; }
    public required List<TaskItem> CompletedTasks { get; init; }
    public required List<TaskItem> TodayTasks { get; init; }
    public int PendingCount => PendingTasks.Count;
    public int CompletedCount => CompletedTasks.Count;
}
