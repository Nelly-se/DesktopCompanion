// =============================================================================
// SpiritDefinition.cs — EF 实体：精灵静态配置（表 Spirits，主键 string Id）
// =============================================================================
// 数据结构：宽表，一行一只精灵；启动时 HasData 种子写入，运行时主要只读
// C# 语法：
//   - string 主键：非自增，Id 与 SpiritIds 常量一致
//   - int 加成字段：业务层做算术；bool WelcomeBackCompensation 存 SQLite 0/1
// =============================================================================

namespace SpiritDesk.Core.Entities;

/// <summary>一只精灵的元数据与数值加成。</summary>
public class SpiritDefinition
{
    /// <summary>精灵唯一 ID，见 <see cref="Constants.SpiritIds"/>。</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>MBTI 等人设标签，用于展示与 LLM system prompt。</summary>
    public string Mbti { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string CoreRole { get; set; } = string.Empty;

    public string ElementType { get; set; } = string.Empty;

    public string Personality { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DialogueExample { get; set; } = string.Empty;

    public string SpecialMechanism { get; set; } = string.Empty;

    /// <summary>立绘 URL 路径，如 /assets/images/spirit-light.png。</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>主题色十六进制，如 #7A85FF。</summary>
    public string AccentColor { get; set; } = string.Empty;

    public int CheckInBonusMultiplier { get; set; } = 1;

    public int TaskAffinityBonus { get; set; }

    public int FeedMoodBonus { get; set; }

    public int GameCountBonus { get; set; }

    public int GameCoinBonus { get; set; }

    public int MoodDecayReduction { get; set; }

    public bool WelcomeBackCompensation { get; set; }
}
