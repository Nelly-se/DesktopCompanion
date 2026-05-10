using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Services;

public class SpiritPersonaService
{
    public string BuildGreeting(SpiritDefinition spirit, string nickname)
    {
        var name = string.IsNullOrWhiteSpace(nickname) ? "朋友" : nickname.Trim();
        return spirit.Id switch
        {
            SpiritIds.Light => $"下午好，{name}。卷卷晴已就位，今天我们先把最重要的事推起来。",
            SpiritIds.Water => $"下午好，{name}。嘻嘻滴来啦，先把心情补满，再一起往前冲。",
            SpiritIds.Air => $"下午好，{name}。贴贴朵已经准备好陪你连接今天的人和事。",
            SpiritIds.Soil => $"下午好，{name}。慢慢壤在这里，今天也可以慢慢走，但别停下。",
            SpiritIds.Nutrition => $"下午好，{name}。新新星带着新点子来了，我们试试看不一样的路径。",
            _ => $"欢迎回来，{name}。"
        };
    }

    public string BuildWelcomeBack(SpiritDefinition spirit)
    {
        return spirit.Id switch
        {
            SpiritIds.Light => "先确认今天最关键的一件事，然后把它拆开执行。",
            SpiritIds.Water => "先笑一下，今天也值得拥有一个轻松开局。",
            SpiritIds.Air => "也许今天可以顺便问候一下很久没联系的人。",
            SpiritIds.Soil => "先喝口水，调整一下呼吸，事情可以一件一件做。",
            SpiritIds.Nutrition => "今天不妨尝试一种新的做事方式，说不定会有惊喜。",
            _ => "欢迎回来。"
        };
    }

    public string BuildHelperTip(SpiritDefinition spirit)
    {
        return spirit.Id switch
        {
            SpiritIds.Light => "效率提示：先完成一件最难的小事，能明显提升后续推进感。",
            SpiritIds.Water => "快乐提示：把今天的一件小开心写下来，情绪会更稳定。",
            SpiritIds.Air => "社交提示：一句简短问候，也可能修复一段关系的距离。",
            SpiritIds.Soil => "养护提示：久坐 40 分钟后起来活动两分钟，身体会更舒服。",
            SpiritIds.Nutrition => "探索提示：把老任务换个做法，往往比硬扛更有效。",
            _ => "今天也继续加油。"
        };
    }

    public string GenerateReply(SpiritDefinition spirit, string message)
    {
        var text = message.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "你可以告诉我，今天最想先处理什么。";
        }

        if (ContainsAny(text, "任务", "作业", "学习", "计划"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "这个任务很有挑战性，需要我帮你拆成三步吗？",
                SpiritIds.Water => "先别把自己吓到，我们边做边找点开心节奏，做完记得奖励自己一下。",
                SpiritIds.Air => "做事之前，也别忘了看看有没有需要沟通和协作的地方。",
                SpiritIds.Soil => "别急，我们把节奏放稳，一步一步完成就很好。",
                SpiritIds.Nutrition => "要不要试试用不同的方法完成今天的任务？",
                _ => "我们先把任务拆开。"
            };
        }

        if (ContainsAny(text, "答辩", "展示", "演示", "汇报", "论文"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "答辩最稳的顺序是：项目定位、核心闭环、关键功能演示、技术选型、亮点与取舍。要我帮你压成一分钟话术吗？",
                SpiritIds.Water => "先别紧张，答辩不是考试，是带老师看你们已经做成了什么。我们把演示顺顺就会轻松很多。",
                SpiritIds.Air => "答辩时记得强调团队分工和协作方式，这会让项目完整度更高。",
                SpiritIds.Soil => "先把呼吸放慢一点，只要流程熟悉、语速稳定，你的表达就会越来越顺。",
                SpiritIds.Nutrition => "答辩也可以像讲一个产品故事，先讲为什么做，再讲怎么做，最后讲做出了什么。",
                _ => "我们可以先把答辩内容拆成几个自然段。"
            };
        }

        if (ContainsAny(text, "总结", "复盘", "概括", "梳理"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "可以，我建议按“目标、动作、结果、下一步”四段来总结，最清晰。",
                SpiritIds.Water => "总结也别太严肃，我们可以先抓住今天最值得开心的一件事，再看要补什么。",
                SpiritIds.Air => "复盘时也可以想想，今天和别人协作的地方哪里最顺、哪里还能优化。",
                SpiritIds.Soil => "我们可以慢一点梳理，把情绪变化和任务完成情况都放进去。",
                SpiritIds.Nutrition => "不如试试把今天拆成“已验证、待探索、可迭代”三个部分，会更有趣。",
                _ => "我们先把重点列出来。"
            };
        }

        if (ContainsAny(text, "累", "困", "难过", "压力", "焦虑"))
        {
            return spirit.Id switch
            {
                SpiritIds.Soil => "累了就休息一下吧，今天的你已经很棒了。",
                SpiritIds.Water => "不开心？来，先别绷着，我们一起把心情抖一抖。",
                SpiritIds.Air => "如果是关系或沟通卡住了，我们可以先理清你最在意的点。",
                SpiritIds.Light => "先停五分钟，整理状态后再回来推进，比硬扛更有效。",
                SpiritIds.Nutrition => "先别急着否定自己，换个角度看问题，可能会轻松很多。",
                _ => "先照顾好自己。"
            };
        }

        if (ContainsAny(text, "为什么", "怎么会", "原理"))
        {
            return spirit.Id switch
            {
                SpiritIds.Nutrition => "这个问题很适合往深一点想，我们可以先看表象，再追原因，再找替代路径。",
                SpiritIds.Light => "先把目标和条件说清楚，再分析原因会更高效。",
                SpiritIds.Water => "也许答案不只一个，我们先从你最感兴趣的角度切进去。",
                SpiritIds.Air => "这个问题也可能和人或场景有关，我们可以一起把语境补完整。",
                SpiritIds.Soil => "慢一点想没关系，把前因后果捋顺就会清楚很多。",
                _ => "我们先把原因拆开看。"
            };
        }

        if (ContainsAny(text, "你好", "在吗", "陪我"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "在，卷卷晴已经上线。别刷手机了，今天的番茄钟还没完成哦。",
                SpiritIds.Water => "在呀，嘻嘻滴今天状态超好，想先分享开心事还是先吐槽？",
                SpiritIds.Air => "我在，贴贴朵会认真听你说，也会提醒你照顾重要的人。",
                SpiritIds.Soil => "我一直都在，慢慢来，说到哪都可以。",
                SpiritIds.Nutrition => "我在，我最近发现了一个超酷的知识点，想知道吗？",
                _ => "我在。"
            };
        }

        return spirit.Id switch
        {
            SpiritIds.Light => "收到，我们先明确目标，再决定下一步动作。",
            SpiritIds.Water => "先把心情稳住一点，再往前走会更舒服。",
            SpiritIds.Air => "这个话题也许可以从人与关系的角度再想一步。",
            SpiritIds.Soil => "不必一下做到完美，稳定推进就已经很好。",
            SpiritIds.Nutrition => "这个点挺有意思，我建议你大胆试一个新方法。",
            _ => "我在认真听你说。"
        };
    }

    public string BuildInteractionReply(SpiritDefinition spirit, string action)
    {
        return (spirit.Id, action) switch
        {
            (SpiritIds.Light, "checkin") => "卷卷晴记下你今天的开场了，接下来该推进正事了。",
            (SpiritIds.Water, "feed") => "嘻嘻滴收到补给啦，心情值正在蹭蹭上涨。",
            (SpiritIds.Air, "checkin") => "贴贴朵会把今天也当作值得认真联结的一天。",
            (SpiritIds.Soil, "rest") => "慢慢壤提醒你，休息不是偷懒，而是在照顾长期状态。",
            (SpiritIds.Nutrition, "study") => "新新星建议你试试换一种学习顺序，也许会更顺手。",
            (_, "encourage") => $"{spirit.Name} 认真看着你：你已经比想象中更努力了。",
            (_, "study") => $"{spirit.Name} 已切换到陪学模式，我们先专注 25 分钟。",
            (_, "rest") => $"{spirit.Name} 提醒你松一松肩膀，休息也是前进的一部分。",
            (_, "feed") => $"{spirit.Name} 开心地收下了你的投喂，气氛一下子暖起来了。",
            _ => $"{spirit.Name} 记下了你今天的到来，这就是很好的开始。"
        };
    }

    public string BuildGameReply(SpiritDefinition spirit, string outcome)
    {
        return outcome switch
        {
            "win" => $"{spirit.Name} 被你赢啦，不过它好像也从这局里学到了新点子。",
            "draw" => $"{spirit.Name} 和你打成平手，今天的节奏刚刚好。",
            _ => $"{spirit.Name} 小赢一局，但还是把鼓励留给你。"
        };
    }

    private static bool ContainsAny(string input, params string[] keywords)
    {
        return keywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
