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
            SpiritIds.Light => $"你好，{name}。卷卷晴已就位，先把最重要的一件事推进起来。",
            SpiritIds.Water => $"你好，{name}。嘻嘻滴来啦，先补一点快乐能量再出发。",
            SpiritIds.Air => $"你好，{name}。贴贴朵在这里，今天也照顾好人与事的连接。",
            SpiritIds.Soil => $"你好，{name}。慢慢壤在这里，今天也可以慢慢来，但别停下。",
            SpiritIds.Nutrition => $"你好，{name}。新新星带着新点子来了，我们试试不一样的路径。",
            _ => $"欢迎回来，{name}。"
        };
    }

    public string BuildWelcomeBack(SpiritDefinition spirit)
    {
        return spirit.Id switch
        {
            SpiritIds.Light => "先确认今天最关键的一件事，然后拆开执行。",
            SpiritIds.Water => "先笑一下，今天也值得一个轻松开局。",
            SpiritIds.Air => "也许今天可以顺便问候一位很久没联系的人。",
            SpiritIds.Soil => "先喝口水，调一调呼吸，事情可以一件一件做。",
            SpiritIds.Nutrition => "今天不妨尝试一种新的做事方式，也许会有惊喜。",
            _ => "欢迎回来。"
        };
    }

    public string BuildHelperTip(SpiritDefinition spirit)
    {
        return spirit.Id switch
        {
            SpiritIds.Light => "效率提示：先完成最关键的小任务，能明显提升推进感。",
            SpiritIds.Water => "快乐提示：记下一件小开心，情绪会更稳定。",
            SpiritIds.Air => "社交提示：一句简短问候，可能就能拉近关系。",
            SpiritIds.Soil => "养护提示：久坐 40 分钟后起来活动 2 分钟。",
            SpiritIds.Nutrition => "探索提示：换一种做法，常比硬扛更有效。",
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
                SpiritIds.Water => "先别把自己吓到，我们边做边找点轻松节奏，做完记得奖励自己。",
                SpiritIds.Air => "做事之前，也别忘了看看有没有需要沟通协作的地方。",
                SpiritIds.Soil => "别急，我们把节奏放稳，一步一步完成就很好。",
                SpiritIds.Nutrition => "要不要试试用不同的方法完成今天的任务？",
                _ => "我们先把任务拆开。"
            };
        }

        if (ContainsAny(text, "答辩", "展示", "演示", "汇报", "论文"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "答辩顺序建议：项目定位、核心闭环、关键功能演示、技术选型、取舍与亮点。",
                SpiritIds.Water => "先别紧张，答辩是带老师看你们做成了什么，不是考试。",
                SpiritIds.Air => "记得强调团队分工与协作方式，会让项目完整度更高。",
                SpiritIds.Soil => "保持呼吸和语速稳定，表达会更顺。",
                SpiritIds.Nutrition => "可以按产品故事讲：为什么做、怎么做、做成了什么。",
                _ => "我们可以先把答辩内容拆成几个自然段。"
            };
        }

        if (ContainsAny(text, "累", "困", "难过", "压力", "焦虑"))
        {
            return spirit.Id switch
            {
                SpiritIds.Soil => "累了就先停一下，照顾好自己也很重要。",
                SpiritIds.Water => "来，先把心情抖一抖，我们慢慢来。",
                SpiritIds.Air => "如果是沟通卡住了，我们先理清你最在意的点。",
                SpiritIds.Light => "先休息五分钟，再回来推进，效率会更高。",
                SpiritIds.Nutrition => "换个角度看问题，可能会轻松很多。",
                _ => "先照顾好自己。"
            };
        }

        if (ContainsAny(text, "你好", "在吗", "陪我"))
        {
            return spirit.Id switch
            {
                SpiritIds.Light => "在，卷卷晴在线。今天的关键事项准备开动了吗？",
                SpiritIds.Water => "在呀，嘻嘻滴状态满格。先分享一个开心点？",
                SpiritIds.Air => "我在，贴贴朵会认真听你说。",
                SpiritIds.Soil => "我一直都在，慢慢来就好。",
                SpiritIds.Nutrition => "我在，新新星今天也准备好一起探索了。",
                _ => "我在。"
            };
        }

        return spirit.Id switch
        {
            SpiritIds.Light => "收到，我们先明确目标，再决定下一步动作。",
            SpiritIds.Water => "先把心情稳住一点，再往前走会更舒服。",
            SpiritIds.Air => "这个话题也可以从人与关系角度再想一步。",
            SpiritIds.Soil => "不必一下做到完美，稳定推进就很好。",
            SpiritIds.Nutrition => "这个点很有意思，建议大胆试一个新方法。",
            _ => "我在认真听你说。"
        };
    }

    public string BuildInteractionReply(SpiritDefinition spirit, string action)
    {
        return (spirit.Id, action) switch
        {
            (SpiritIds.Light, "checkin") => "卷卷晴记录了你今天的开场，继续推进吧。",
            (SpiritIds.Water, "feed") => "嘻嘻滴收到了投喂，快乐能量上升中。",
            (SpiritIds.Air, "checkin") => "贴贴朵记下了今天这次认真打卡。",
            (SpiritIds.Soil, "rest") => "慢慢壤提醒你：休息不是偷懒，是长期状态管理。",
            (SpiritIds.Nutrition, "study") => "新新星建议你试试换一种学习顺序。",
            (_, "encourage") => $"{spirit.Name} 看着你说：你已经比想象中更努力了。",
            (_, "study") => $"{spirit.Name} 已切换到陪学模式，我们先专注 25 分钟。",
            (_, "rest") => $"{spirit.Name} 提醒你放松一下肩颈和眼睛。",
            (_, "feed") => $"{spirit.Name} 开心地收下了你的投喂。",
            _ => $"{spirit.Name} 记下了你今天的到来，这是很好的开始。"
        };
    }

    public string BuildGameReply(SpiritDefinition spirit, string outcome)
    {
        return outcome switch
        {
            "win" => $"{spirit.Name} 被你赢啦，不过它好像也学到新点子了。",
            "draw" => $"{spirit.Name} 和你打成平手，节奏刚刚好。",
            _ => $"{spirit.Name} 小赢一局，但还是把鼓励留给你。"
        };
    }

    private static bool ContainsAny(string input, params string[] keywords)
    {
        return keywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
