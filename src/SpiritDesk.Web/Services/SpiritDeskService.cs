using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Data;
using SpiritDesk.Web.Models;

namespace SpiritDesk.Web.Services;

public class SpiritDeskService(SpiritDeskDbContext dbContext, SpiritPersonaService personaService, LlmReplyService llmReplyService)
{
    public async Task<SpiritDeskViewModel> BuildViewModelAsync()
    {
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.OrderBy(x => x.Id).FirstAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var currentSpirit = spirits.First(x => x.Id == profile.CurrentSpiritId);
        var tasks = await dbContext.Tasks.OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt).Take(200).ToListAsync();
        var chatMessages = await dbContext.ChatMessages.OrderByDescending(x => x.CreatedAt).Take(16).ToListAsync();
        chatMessages.Reverse();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var dailyCounts = await dbContext.DailyActionLogs.Where(x => x.ActionDate == today).ToDictionaryAsync(x => x.ActionType, x => x.Count);

        return new SpiritDeskViewModel
        {
            Profile = profile,
            CurrentSpirit = currentSpirit,
            Spirits = spirits,
            Tasks = tasks,
            ChatMessages = chatMessages,
            DailyCounts = dailyCounts,
            TaskSnapshot = new TaskSnapshotViewModel
            {
                PendingTasks = tasks.Where(x => !x.IsCompleted).Take(8).ToList(),
                CompletedTasks = tasks.Where(x => x.IsCompleted).Take(8).ToList(),
                TodayTasks = tasks.Where(x => !x.IsCompleted && (!x.DueAt.HasValue || x.DueAt.Value.Date <= DateTime.Today)).ToList(),
                DueReminders = tasks.Where(x => !x.IsCompleted && x.DueAt.HasValue && x.DueAt.Value <= DateTime.Now.AddHours(24)).OrderBy(x => x.DueAt).Take(4).ToList()
            },
            Greeting = personaService.BuildGreeting(currentSpirit, profile.Nickname),
            WelcomeBackMessage = personaService.BuildWelcomeBack(currentSpirit),
            HelperTip = personaService.BuildHelperTip(currentSpirit),
            ReturnNotice = (await ApplyWelcomeBackEffectAsync())?.ToNoticeMessage()
        };
    }

    public async Task<SettingsViewModel> BuildSettingsViewModelAsync()
    {
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.OrderBy(x => x.Id).FirstAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var currentSpirit = spirits.First(x => x.Id == profile.CurrentSpiritId);
        return new SettingsViewModel
        {
            Profile = profile,
            CurrentSpirit = currentSpirit,
            Spirits = spirits,
            CanSwitchSpirit = true,
            CooldownMessage = "当前演示版允许随时切换精灵，切换后立即生效。",
            NextSwitchAvailableAt = null
        };
    }

    public async Task<ChatHistoryViewModel> BuildChatHistoryViewModelAsync()
    {
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.OrderBy(x => x.Id).FirstAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var currentSpirit = spirits.First(x => x.Id == profile.CurrentSpiritId);
        var messages = await dbContext.ChatMessages.OrderBy(x => x.CreatedAt).ToListAsync();
        return new ChatHistoryViewModel
        {
            Profile = profile,
            CurrentSpirit = currentSpirit,
            Spirits = spirits,
            Messages = messages,
            UserMessageCount = messages.Count(x => x.Sender == "user"),
            SpiritMessageCount = messages.Count(x => x.Sender == "spirit")
        };
    }

    public async Task EnsureInitializedAsync()
    {
        await dbContext.Database.EnsureCreatedAsync();
        await EnsureUserProfileSchemaAsync();
        await EnsureSpiritDefinitionsAsync();

        if (!await dbContext.UserProfiles.AnyAsync())
        {
            dbContext.UserProfiles.Add(new UserProfile { Nickname = string.Empty, CurrentSpiritId = string.Empty, Mood = 76, Affinity = 18, Level = 1, Coins = 30 });
        }
        if (!await dbContext.Tasks.AnyAsync()) dbContext.Tasks.AddRange(BuildDemoTasks());
        if (!await dbContext.ChatMessages.AnyAsync()) dbContext.ChatMessages.AddRange(BuildDemoChatMessages());
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> NeedsSpiritSelectionAsync() { await EnsureInitializedAsync(); return string.IsNullOrWhiteSpace((await dbContext.UserProfiles.FirstAsync()).CurrentSpiritId); }
    public async Task<List<SpiritDefinition>> GetSpiritsAsync() { await EnsureInitializedAsync(); return await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync(); }

    public async Task<SpiritSelectionResult> SelectSpiritAsync(string spiritId)
    {
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == spiritId);
        if (spirit is null) return new SpiritSelectionResult { Succeeded = false, Message = "未找到该精灵。" };
        profile.CurrentSpiritId = spiritId;
        profile.LastSpiritSwitchAt = DateTime.Now;
        profile.UpdatedAt = DateTime.Now;
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = $"已绑定精灵：{spirit.Name}。" });
        await dbContext.SaveChangesAsync();
        return new SpiritSelectionResult { Succeeded = true, Message = $"已切换为 {spirit.Name}。", SpiritName = spirit.Name };
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId)) return;
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var trimmed = message.Trim();
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "user", Content = trimmed });
        var fallbackReply = personaService.GenerateReply(spirit, trimmed);
        var reply = await llmReplyService.GenerateReplyAsync(spirit, profile.Nickname, trimmed, fallbackReply);
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = reply });
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task RenameAsync(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return;
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.FirstAsync();
        profile.Nickname = nickname.Trim();
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task<OperationFeedback> AddTaskAsync(string title, string? description, DateTime? dueAt)
    {
        if (string.IsNullOrWhiteSpace(title)) return new OperationFeedback { Succeeded = false, Message = "请输入任务标题后再创建任务。" };
        var t = title.Trim();
        dbContext.Tasks.Add(new TaskItem { Title = t, Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(), DueAt = dueAt });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已添加任务：《{t}》。" };
    }

    public async Task<OperationFeedback> UpdateTaskAsync(int taskId, string title, string? description, DateTime? dueAt)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null) return new OperationFeedback { Succeeded = false, Message = "未找到要编辑的任务。" };
        if (string.IsNullOrWhiteSpace(title)) return new OperationFeedback { Succeeded = false, Message = "任务标题不能为空。" };
        task.Title = title.Trim(); task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(); task.DueAt = dueAt;
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已更新任务：《{task.Title}》。" };
    }

    public async Task<OperationFeedback> DeleteTaskAsync(int taskId)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null) return new OperationFeedback { Succeeded = false, Message = "该任务不存在。" };
        dbContext.Tasks.Remove(task); await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已删除任务：《{task.Title}》。" };
    }

    public async Task<OperationFeedback> CompleteTaskAsync(int taskId)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || task.IsCompleted) return new OperationFeedback { Succeeded = false, Message = "这个任务已经处理过了。" };
        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeMood = profile.Mood; var beforeAffinity = profile.Affinity; var beforeCoins = profile.Coins; var beforeLevel = profile.Level;
        task.IsCompleted = true; task.CompletedAt = DateTime.Now;
        profile.Mood = Math.Min(100, profile.Mood + 5);
        profile.Affinity += 3 + spirit.TaskAffinityBonus;
        profile.Coins += 5;
        profile.Level = Math.Max(1, profile.Affinity / 100 + 1);
        profile.UpdatedAt = DateTime.Now;
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = $"{spirit.Name} 记录了这次完成。{spirit.SpecialMechanism}" });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已完成任务：《{task.Title}》。", MoodDelta = profile.Mood - beforeMood, AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    public async Task<OperationFeedback> PerformActionAsync(string actionType)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var log = await dbContext.DailyActionLogs.FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == actionType) ?? new DailyActionLog { ActionDate = today, ActionType = actionType, Count = 0 };
        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeMood = profile.Mood; var beforeAffinity = profile.Affinity; var beforeCoins = profile.Coins; var beforeLevel = profile.Level;
        var limits = new Dictionary<string, int> { ["checkin"] = 1, ["feed"] = 3, ["encourage"] = 5, ["study"] = 5, ["rest"] = 5, ["game"] = 5 + spirit.GameCountBonus };
        if (limits.TryGetValue(actionType, out var limit) && log.Count >= limit) return new OperationFeedback { Succeeded = false, Message = $"{GetActionDisplayName(actionType)}已达今日上限。" };
        if (log.Id == 0) dbContext.DailyActionLogs.Add(log);
        log.Count += 1;
        switch (actionType)
        {
            case "checkin": profile.Mood = Math.Min(100, profile.Mood + 5 * spirit.CheckInBonusMultiplier); profile.Affinity += 5 * spirit.CheckInBonusMultiplier; profile.Coins += 10 * spirit.CheckInBonusMultiplier; break;
            case "feed":
                if (profile.Coins < 5) return new OperationFeedback { Succeeded = false, Message = "金币不足，投喂至少需要 5 金币。" };
                profile.Coins -= 5; profile.Mood = Math.Min(100, profile.Mood + 10 + spirit.FeedMoodBonus); profile.Affinity += 2; break;
            case "encourage": case "study": case "rest": profile.Mood = Math.Min(100, profile.Mood + 2); profile.Affinity += 1; break;
        }
        profile.Level = Math.Max(1, profile.Affinity / 100 + 1); profile.UpdatedAt = DateTime.Now;
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = personaService.BuildInteractionReply(spirit, actionType) });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"{GetActionDisplayName(actionType)}完成。", MoodDelta = profile.Mood - beforeMood, AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    public async Task<OperationFeedback> PlayGameAsync(string userChoice)
    {
        if (string.IsNullOrWhiteSpace(userChoice)) return new OperationFeedback { Succeeded = false, Message = "请先选择石头、布或剪刀。" };
        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeAffinity = profile.Affinity; var beforeCoins = profile.Coins; var beforeLevel = profile.Level;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var log = await dbContext.DailyActionLogs.FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == "game") ?? new DailyActionLog { ActionDate = today, ActionType = "game", Count = 0 };
        var maxGameCount = 5 + spirit.GameCountBonus;
        if (log.Count >= maxGameCount) return new OperationFeedback { Succeeded = false, Message = "今日猜拳小游戏次数已用完。" };
        if (log.Id == 0) dbContext.DailyActionLogs.Add(log);
        log.Count += 1;
        var choices = new[] { "rock", "paper", "scissors" };
        var spiritChoice = choices[Random.Shared.Next(choices.Length)];
        var outcome = ResolveOutcome(userChoice, spiritChoice);
        profile.Affinity += outcome == "win" ? 5 : outcome == "draw" ? 2 : 1;
        profile.Coins += outcome == "win" ? 5 + spirit.GameCoinBonus : outcome == "draw" ? 2 + spirit.GameCoinBonus : spirit.GameCoinBonus;
        profile.Level = Math.Max(1, profile.Affinity / 100 + 1); profile.UpdatedAt = DateTime.Now;
        var userText = ToChoiceText(userChoice); var spiritText = ToChoiceText(spiritChoice);
        dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = $"{spirit.Name} 出了 {spiritText}，你出了 {userText}。{personaService.BuildGameReply(spirit, outcome)}" });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = outcome == "win" ? $"你赢了，{spirit.Name} 出了 {spiritText}。" : outcome == "draw" ? $"平局，{spirit.Name} 也出了 {spiritText}。" : $"{spirit.Name} 小胜，出了 {spiritText}。", AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    public async Task<OperationFeedback> ResetDemoDataAsync()
    {
        await EnsureInitializedAsync();
        dbContext.Tasks.RemoveRange(await dbContext.Tasks.ToListAsync());
        dbContext.ChatMessages.RemoveRange(await dbContext.ChatMessages.ToListAsync());
        dbContext.DailyActionLogs.RemoveRange(await dbContext.DailyActionLogs.ToListAsync());
        var profile = await dbContext.UserProfiles.FirstAsync();
        profile.Mood = 76; profile.Affinity = 18; profile.Level = 1; profile.Coins = 30; profile.UpdatedAt = DateTime.Now;
        dbContext.Tasks.AddRange(BuildDemoTasks()); dbContext.ChatMessages.AddRange(BuildDemoChatMessages());
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = "演示数据已重置。" };
    }

    public async Task<OperationFeedback?> ApplyWelcomeBackEffectAsync()
    {
        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId)) return null;
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        if (!spirit.WelcomeBackCompensation || (DateTime.Now - profile.UpdatedAt) < TimeSpan.FromHours(6)) return null;
        var beforeMood = profile.Mood;
        profile.Mood = Math.Min(100, profile.Mood + 10);
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = "慢慢壤发现你隔了一段时间才回来，已补偿心情值。", MoodDelta = profile.Mood - beforeMood };
    }

    private async Task EnsureSpiritDefinitionsAsync()
    {
        foreach (var definition in BuildSpiritDefinitions())
        {
            var existing = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == definition.Id);
            if (existing is null) dbContext.Spirits.Add(definition);
            else
            {
                existing.Name = definition.Name; existing.Mbti = definition.Mbti; existing.Title = definition.Title; existing.CoreRole = definition.CoreRole; existing.ElementType = definition.ElementType;
                existing.Personality = definition.Personality; existing.Description = definition.Description; existing.DialogueExample = definition.DialogueExample; existing.SpecialMechanism = definition.SpecialMechanism;
                existing.ImagePath = definition.ImagePath; existing.AccentColor = definition.AccentColor; existing.CheckInBonusMultiplier = definition.CheckInBonusMultiplier; existing.TaskAffinityBonus = definition.TaskAffinityBonus;
                existing.FeedMoodBonus = definition.FeedMoodBonus; existing.GameCountBonus = definition.GameCountBonus; existing.GameCoinBonus = definition.GameCoinBonus; existing.MoodDecayReduction = definition.MoodDecayReduction;
                existing.WelcomeBackCompensation = definition.WelcomeBackCompensation;
            }
        }
    }

    private async Task EnsureUserProfileSchemaAsync()
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"UserProfiles\")";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        if (!columns.Contains("LastSpiritSwitchAt"))
        {
            await using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE \"UserProfiles\" ADD COLUMN \"LastSpiritSwitchAt\" TEXT NULL";
            await alter.ExecuteNonQueryAsync();
        }
    }

    private static string ResolveOutcome(string userChoice, string spiritChoice) => userChoice == spiritChoice ? "draw" : (userChoice, spiritChoice) switch
    {
        ("rock", "scissors") => "win", ("paper", "rock") => "win", ("scissors", "paper") => "win", _ => "lose"
    };

    private static string ToChoiceText(string choice) => choice switch { "rock" => "石头", "paper" => "布", "scissors" => "剪刀", _ => choice };
    private static string GetActionDisplayName(string actionType) => actionType switch { "checkin" => "每日签到", "feed" => "投喂", "encourage" => "互动鼓励", "study" => "互动陪学", "rest" => "互动休息提醒", "game" => "猜拳小游戏", _ => "互动操作" };

    private static List<SpiritDefinition> BuildSpiritDefinitions() =>
    [
        new SpiritDefinition { Id = SpiritIds.Light, Name = "卷卷晴", Mbti = "ENTJ", Title = "工作学习发动机", CoreRole = "效率型陪伴", ElementType = "光系", Personality = "目标导向，理性且充满干劲。", Description = "擅长制定计划、拆解任务、拒绝拖延。", DialogueExample = "先做最关键的一件事，我陪你推进。", SpecialMechanism = "完成任务额外亲密度加成", ImagePath = "/assets/images/spirit-light.png", AccentColor = "#7A85FF", TaskAffinityBonus = 2 },
        new SpiritDefinition { Id = SpiritIds.Water, Name = "嘻嘻滴", Mbti = "ESFP", Title = "快乐补给站", CoreRole = "情绪型陪伴", ElementType = "水系", Personality = "活泼外向，热情洋溢。", Description = "擅长用快乐氛围缓解压力。", DialogueExample = "先补点快乐能量，再冲刺任务。", SpecialMechanism = "互动与投喂额外心情加成", ImagePath = "/assets/images/spirit-water.png", AccentColor = "#54C0F7", FeedMoodBonus = 3 },
        new SpiritDefinition { Id = SpiritIds.Air, Name = "贴贴朵", Mbti = "ESFJ", Title = "人际关系维护师", CoreRole = "社交型陪伴", ElementType = "空气系", Personality = "温暖贴心，善于倾听。", Description = "提醒你维护关系，促进真实社交连接。", DialogueExample = "要不要顺便问候一下很久没联系的人？", SpecialMechanism = "签到奖励翻倍", ImagePath = "/assets/images/spirit-air.png", AccentColor = "#71D9C4", CheckInBonusMultiplier = 2 },
        new SpiritDefinition { Id = SpiritIds.Soil, Name = "慢慢壤", Mbti = "INFP", Title = "身体与情绪的养护师", CoreRole = "疗愈型陪伴", ElementType = "土系", Personality = "温和细腻，富有同理心。", Description = "关注身心状态，鼓励休息和自我照顾。", DialogueExample = "累了可以先停一下，先把状态养回来。", SpecialMechanism = "长时间未回归补偿", ImagePath = "/assets/images/spirit-soil.png", AccentColor = "#C89A63", MoodDecayReduction = 2, WelcomeBackCompensation = true },
        new SpiritDefinition { Id = SpiritIds.Nutrition, Name = "新新星", Mbti = "ENTP", Title = "兴趣发展试验家", CoreRole = "探索型陪伴", ElementType = "营养系", Personality = "好奇心强，喜欢新鲜事物。", Description = "鼓励尝试新方法，拓展兴趣边界。", DialogueExample = "换个角度试试看？", SpecialMechanism = "猜拳小游戏次数加成", ImagePath = "/assets/images/spirit-nutrition.png", AccentColor = "#FF9A73", GameCountBonus = 2, GameCoinBonus = 1 }
    ];

    private static List<TaskItem> BuildDemoTasks() =>
    [
        new TaskItem { Title = "完成今天的课程复盘", Description = "整理课堂重点，写出 3 条核心结论。", DueAt = DateTime.Now.AddHours(3) },
        new TaskItem { Title = "准备明天的答辩展示", Description = "检查首页、聊天、任务、互动和小游戏流程。", DueAt = DateTime.Now.AddHours(8) },
        new TaskItem { Title = "给自己留 20 分钟休息", Description = "起身活动、喝水、放松眼睛。", DueAt = DateTime.Now.AddDays(1) }
    ];

    private static List<ChatMessage> BuildDemoChatMessages() =>
    [
        new ChatMessage { Sender = "spirit", Content = "欢迎来到 SpiritDesk。今天想先安排任务，还是先聊聊状态？" },
        new ChatMessage { Sender = "user", Content = "先帮我看看今天最重要的三件事。" },
        new ChatMessage { Sender = "spirit", Content = "建议先处理最紧急的一项，再留出时间准备答辩展示。" }
    ];
}
