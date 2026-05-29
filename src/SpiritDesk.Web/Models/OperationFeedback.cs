// =============================================================================
// OperationFeedback.cs — 业务操作统一返回结构（签到/投喂/猜拳/任务等）
// =============================================================================
// 数据结构：值类型 bool/int + string Message；非 EF 实体，Service 方法返回值
// C# 语法：
//   - init：调用方用对象初始化器 new OperationFeedback { Succeeded = true, ... }
//   - List&lt;string&gt; deltas：局部集合，string.Join 拼横幅
//   - 三元运算符 (MoodDelta > 0 ? "+" : string.Empty)：正数前加加号
// =============================================================================

namespace SpiritDesk.Web.Models;

/// <summary>SpiritDeskService 互动/任务方法返回；ToNoticeMessage 供 TempData 横幅。</summary>
public class OperationFeedback
{
    public bool Succeeded { get; init; }
    public required string Message { get; init; }
    public int MoodDelta { get; init; }
    public int AffinityDelta { get; init; }
    public int CoinsDelta { get; init; }
    public int LevelDelta { get; init; }

    /// <summary>将数值变化追加到主消息，无变化则只返回 Message。</summary>
    public string ToNoticeMessage()
    {
        // List<T>：动态数组，按添加顺序保存字符串片段
        var deltas = new List<string>();

        if (MoodDelta != 0)
        {
            deltas.Add($"心情 {(MoodDelta > 0 ? "+" : string.Empty)}{MoodDelta}");
        }

        if (AffinityDelta != 0)
        {
            deltas.Add($"亲密度 {(AffinityDelta > 0 ? "+" : string.Empty)}{AffinityDelta}");
        }

        if (CoinsDelta != 0)
        {
            deltas.Add($"金币 {(CoinsDelta > 0 ? "+" : string.Empty)}{CoinsDelta}");
        }

        if (LevelDelta != 0)
        {
            deltas.Add($"等级 {(LevelDelta > 0 ? "+" : string.Empty)}{LevelDelta}");
        }

        // 三元运算符：deltas 为空则 Message，否则用 " · " 连接
        return deltas.Count == 0
            ? Message
            : $"{Message} · {string.Join(" · ", deltas)}";
    }
}
