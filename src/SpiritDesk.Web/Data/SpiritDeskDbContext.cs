using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;

namespace SpiritDesk.Web.Data;

public class SpiritDeskDbContext(DbContextOptions<SpiritDeskDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<WebAccount> WebAccounts => Set<WebAccount>();
    public DbSet<SpiritDefinition> Spirits => Set<SpiritDefinition>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DailyActionLog> DailyActionLogs => Set<DailyActionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<SpiritDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<DailyActionLog>().Property(x => x.ActionDate).HasConversion(
            value => value.ToDateTime(TimeOnly.MinValue),
            value => DateOnly.FromDateTime(value));

        modelBuilder.Entity<SpiritDefinition>().HasData(
            new SpiritDefinition
            {
                Id = SpiritIds.Light,
                Name = "卷卷晴",
                Mbti = "ENTJ",
                Title = "工作学习发动机",
                CoreRole = "效率型陪伴",
                ElementType = "光系",
                Personality = "目标导向，理性且充满干劲。",
                Description = "擅长制定计划、拆解任务、拒绝拖延，像一位严格但尽责的私人小教练。",
                DialogueExample = "这个任务很有挑战性，需要我帮你拆解成三步吗？别刷手机了，今天的番茄钟还没完成哦！",
                SpecialMechanism = "完成任务时亲密度额外 +2；效率类问题会给出更具体的推进建议。",
                ImagePath = "/assets/images/spirit-light.png",
                AccentColor = "#7A85FF",
                TaskAffinityBonus = 2
            },
            new SpiritDefinition
            {
                Id = SpiritIds.Water,
                Name = "嘻嘻滴",
                Mbti = "ESFP",
                Title = "快乐补给站",
                CoreRole = "情绪型陪伴",
                ElementType = "水系",
                Personality = "活泼外向，热情洋溢。",
                Description = "擅长用快乐感感染他人，总能找到生活里好玩的事情，是气氛担当。",
                DialogueExample = "哇！今天有什么开心的事分享给我吗？不开心？来，跟我一起做个鬼脸，哈哈！",
                SpecialMechanism = "互动和投喂时心情值额外 +3；优先给出积极情绪反馈。",
                ImagePath = "/assets/images/spirit-water.png",
                AccentColor = "#54C0F7",
                FeedMoodBonus = 3
            },
            new SpiritDefinition
            {
                Id = SpiritIds.Air,
                Name = "贴贴朵",
                Mbti = "ESFJ",
                Title = "人际关系维护师",
                CoreRole = "社交型陪伴",
                ElementType = "空气系",
                Personality = "温暖贴心，善于倾听，重视人际联结。",
                Description = "会提醒你关心朋友和家人，帮助你在忙碌里维持真实的社交连接。",
                DialogueExample = "你好像很久没联系好朋友了，要不要发个消息问候一下？偶尔示弱，也是一种勇敢哦。",
                SpecialMechanism = "签到奖励翻倍；更擅长提供温和的人际沟通建议。",
                ImagePath = "/assets/images/spirit-air.png",
                AccentColor = "#71D9C4",
                CheckInBonusMultiplier = 2
            },
            new SpiritDefinition
            {
                Id = SpiritIds.Soil,
                Name = "慢慢壤",
                Mbti = "INFP",
                Title = "身体与情绪的养护师",
                CoreRole = "疗愈型陪伴",
                ElementType = "土系",
                Personality = "温和细腻，敏感而有同理心。",
                Description = "关注你的身心状态，鼓励你休息、放慢节奏、接纳情绪。",
                DialogueExample = "累了就休息一下吧，今天的你已经很棒了。记得喝水哦，身体是你最忠实的朋友。",
                SpecialMechanism = "长时间未登录后回归会获得额外安抚；压力类提问优先给出疗愈反馈。",
                ImagePath = "/assets/images/spirit-soil.png",
                AccentColor = "#C89A63",
                MoodDecayReduction = 2,
                WelcomeBackCompensation = true
            },
            new SpiritDefinition
            {
                Id = SpiritIds.Nutrition,
                Name = "新新星",
                Mbti = "ENTP",
                Title = "兴趣发展试验家",
                CoreRole = "探索型陪伴",
                ElementType = "营养系",
                Personality = "好奇心旺盛，思维跳跃，喜欢新鲜事物。",
                Description = "鼓励你尝试新爱好、拓展知识边界、保持创造力。",
                DialogueExample = "我最近发现了一个超酷的知识点，想知道吗？要不要试试用不同的方式完成今天的任务？",
                SpecialMechanism = "小游戏次数 +2；面对“为什么”类问题时更偏向启发式回应。",
                ImagePath = "/assets/images/spirit-nutrition.png",
                AccentColor = "#FF9A73",
                GameCountBonus = 2,
                GameCoinBonus = 1
            });
    }
}
