// =============================================================================
// SpiritDeskViewModel.cs — 首页 Index 聚合 DTO（非 EF 实体，不直接映射单表）
// =============================================================================
// 数据结构：
//   - 嵌套实体：UserProfile、SpiritDefinition、List&lt;TaskItem&gt;、List&lt;ChatMessage&gt;
//   - 嵌套 ViewModel：TaskSnapshotViewModel
//   - Dictionary&lt;string, int&gt; DailyCounts：今日各 ActionType 次数（哈希表 O(1) 查找）
// C# 语法：
//   - required：构造/对象初始化时必须赋值（C# 11+）
//   - init：初始化后不可再改，适合只读展示模型
//   - string? ReturnNotice：可空，无横幅提示时为 null
// 绑定：Index.cshtml 顶部 @model SpiritDeskViewModel，页面用 @Model.Profile 等访问
// =============================================================================

using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Models;

/// <summary>由 SpiritDeskService.BuildViewModelAsync 组装。</summary>
public class SpiritDeskViewModel
{
    public required UserProfile Profile { get; init; }
    public required SpiritDefinition CurrentSpirit { get; init; }
    public required List<SpiritDefinition> Spirits { get; init; }
    public required TaskSnapshotViewModel TaskSnapshot { get; init; }
    public required List<TaskItem> Tasks { get; init; }
    public required List<ChatMessage> ChatMessages { get; init; }

    /// <summary>键为 ActionType（checkin/feed 等），值为当日次数。</summary>
    public required Dictionary<string, int> DailyCounts { get; init; }

    public required string Greeting { get; init; }
    public required string WelcomeBackMessage { get; init; }
    public required string HelperTip { get; init; }

    public string? ReturnNotice { get; init; }
}
