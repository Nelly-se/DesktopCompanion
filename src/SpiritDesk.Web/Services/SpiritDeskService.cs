// =============================================================================
// SpiritDeskService.cs — 核心业务服务（EF 读写 + ViewModel 组装 + 互动逻辑）
// =============================================================================
// 依赖注入：主构造函数注入 SpiritDeskDbContext、SpiritPersonaService、LlmReplyService
// 数据结构常用：
//   - List&lt;T&gt; / IReadOnlyList：EF ToListAsync 结果
//   - Dictionary&lt;string,int&gt;：ToDictionaryAsync 按 ActionType 聚合每日次数
//   - OperationFeedback / SpiritSelectionResult：方法返回值 DTO
// C# 语法：
//   - async Task&lt;T&gt; + await：异步避免阻塞线程池（数据库/LLM IO）
//   - ?? 空合并：左侧 null 则用右侧（选精灵回退链）
//   - LINQ：Where/OrderBy/Take/FirstOrDefaultAsync 翻译成 SQL
//   - is null / is not null：模式匹配判断引用
// =============================================================================

using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Auth;
using SpiritDesk.Web.Data;
using SpiritDesk.Web.Models;

namespace SpiritDesk.Web.Services;

/// <summary>档案、任务、聊天、精灵、每日互动；PageModel 应薄调用本类。</summary>
public class SpiritDeskService(
    SpiritDeskDbContext dbContext,
    SpiritPersonaService personaService,
    LlmReplyService llmReplyService,
    IHttpContextAccessor httpContextAccessor)
{
    // --- ViewModel 组装：多表查询 → 聚合成一个 SpiritDeskViewModel ---

    public async Task<SpiritDeskViewModel> BuildViewModelAsync()
    {
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        // List<SpiritDefinition>：五精灵配置，按 Id 排序
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        // ?? 链：当前精灵 → 默认光系 → 列表第一个
        var currentSpirit = await GetSelectedSpiritAsync(profile)
            ?? spirits.FirstOrDefault(x => x.Id == SpiritIds.Light)
            ?? spirits.First();
        var tasks = await QueryTasksForProfile(profile).OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt).Take(200).ToListAsync();
        var chatMessages = await GetConversationMessagesAsync(profile, currentSpirit.Id, 16);

        var today = DateOnly.FromDateTime(DateTime.Now);
        // Dictionary：键 ActionType，值 Count；同日多条记录在业务层合并为键值对
        var dailyCounts = await QueryDailyActionLogsForProfile(profile)
            .Where(x => x.ActionDate == today)
            .ToDictionaryAsync(x => x.ActionType, x => x.Count);

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
        var profile = await GetCurrentProfileAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var currentSpirit = await GetSelectedSpiritAsync(profile)
            ?? spirits.FirstOrDefault(x => x.Id == SpiritIds.Light)
            ?? spirits.First();
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

    public async Task<ChatHistoryViewModel> BuildChatHistoryViewModelAsync(string? spiritId = null)
    {
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var selectedSpirit = await GetSelectedSpiritAsync(profile);
        var currentSpirit = spirits.FirstOrDefault(x => x.Id == spiritId)
            ?? selectedSpirit
            ?? spirits.FirstOrDefault(x => x.Id == SpiritIds.Light)
            ?? spirits.First();
        var messages = await QueryChatMessagesForProfile(profile).OrderBy(x => x.CreatedAt).ToListAsync();
        var currentConversationMessages = FilterConversationMessages(messages, currentSpirit.Id);
        var activeThreadCount = messages
            .Where(x => !string.IsNullOrWhiteSpace(x.SpiritId))
            .Select(x => x.SpiritId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (activeThreadCount == 0 && messages.Count > 0)
        {
            activeThreadCount = 1;
        }

        return new ChatHistoryViewModel
        {
            Profile = profile,
            CurrentSpirit = currentSpirit,
            Spirits = spirits,
            Messages = messages,
            CurrentConversationMessages = currentConversationMessages,
            TotalMessageCount = messages.Count,
            ActiveThreadCount = activeThreadCount,
            UserMessageCount = currentConversationMessages.Count(x => x.Sender == "user"),
            SpiritMessageCount = currentConversationMessages.Count(x => x.Sender == "spirit")
        };
    }

    // --- 初始化与精灵选择 ---

    public async Task EnsureInitializedAsync()
    {
        await dbContext.Database.EnsureCreatedAsync();
        await EnsureUserProfileSchemaAsync();
        await EnsureTaskSchemaAsync();
        await EnsureChatMessageSchemaAsync();
        await EnsureDailyActionLogSchemaAsync();
        await EnsureSpiritDefinitionsAsync();

        var profile = await EnsureCurrentProfileAsync();
        if (!await QueryTasksForProfile(profile).AnyAsync()) dbContext.Tasks.AddRange(BuildDemoTasks(profile.Id));
        if (!await QueryChatMessagesForProfile(profile).AnyAsync()) dbContext.ChatMessages.AddRange(BuildDemoChatMessages(profile.Id, profile.CurrentSpiritId));
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> NeedsSpiritSelectionAsync()
    {
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        return await GetSelectedSpiritAsync(profile) is null;
    }

    public async Task<List<SpiritDefinition>> GetSpiritsAsync() { await EnsureInitializedAsync(); return await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync(); }

    public async Task<SpiritSelectionResult> SelectSpiritAsync(string spiritId)
    {
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        var spirit = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == spiritId);
        if (spirit is null) return new SpiritSelectionResult { Succeeded = false, Message = "未找到该精灵。" };
        profile.CurrentSpiritId = spiritId;
        profile.LastSpiritSwitchAt = DateTime.Now;
        profile.UpdatedAt = DateTime.Now;
        dbContext.ChatMessages.Add(new ChatMessage
        {
            UserProfileId = profile.Id,
            Sender = "spirit",
            SpiritId = spirit.Id,
            Content = $"{spirit.Name} 已来到桌面。{personaService.BuildGreeting(spirit, profile.Nickname)}"
        });
        await dbContext.SaveChangesAsync();
        return new SpiritSelectionResult { Succeeded = true, Message = $"已切换为 {spirit.Name}。", SpiritName = spirit.Name };
    }

    // --- 聊天与档案 ---

    public async Task SendMessageAsync(string message, string? spiritId = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        var spirit = await ResolveConversationSpiritAsync(profile, spiritId);
        if (spirit is null) return;
        var recentConversation = await GetConversationMessagesAsync(profile, spirit.Id, 10);
        var trimmed = message.Trim();
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "user", SpiritId = spirit.Id, Content = trimmed });
        var fallbackReply = personaService.GenerateReply(spirit, trimmed);
        var reply = await llmReplyService.GenerateReplyAsync(spirit, profile.Nickname, trimmed, fallbackReply, recentConversation);
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "spirit", SpiritId = spirit.Id, Content = reply });
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task<string?> SendMessageStreamingAsync(
        string message,
        Func<string, Task> onReplyChunk,
        string? spiritId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        var spirit = await ResolveConversationSpiritAsync(profile, spiritId);
        if (spirit is null) return null;
        var recentConversation = await GetConversationMessagesAsync(profile, spirit.Id, 10);
        var trimmed = message.Trim();
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "user", SpiritId = spirit.Id, Content = trimmed });
        await dbContext.SaveChangesAsync(cancellationToken);

        var fallbackReply = personaService.GenerateReply(spirit, trimmed);
        var reply = await llmReplyService.GenerateReplyStreamAsync(
            spirit,
            profile.Nickname,
            trimmed,
            fallbackReply,
            recentConversation,
            onReplyChunk,
            cancellationToken);

        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "spirit", SpiritId = spirit.Id, Content = reply });
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return reply;
    }

    public async Task RenameAsync(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return;
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        profile.Nickname = nickname.Trim();
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    // --- 任务 CRUD ---

    public async Task<OperationFeedback> AddTaskAsync(string title, string? description, DateTime? dueAt)
    {
        if (string.IsNullOrWhiteSpace(title)) return new OperationFeedback { Succeeded = false, Message = "请输入任务标题后再创建任务。" };
        var profile = await GetCurrentProfileAsync();
        var t = title.Trim();
        dbContext.Tasks.Add(new TaskItem { UserProfileId = profile.Id, Title = t, Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(), DueAt = dueAt });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已添加任务：《{t}》。" };
    }

    public async Task<OperationFeedback> UpdateTaskAsync(int taskId, string title, string? description, DateTime? dueAt)
    {
        var profile = await GetCurrentProfileAsync();
        var task = await QueryTasksForProfile(profile).FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null) return new OperationFeedback { Succeeded = false, Message = "未找到要编辑的任务。" };
        if (string.IsNullOrWhiteSpace(title)) return new OperationFeedback { Succeeded = false, Message = "任务标题不能为空。" };
        task.Title = title.Trim(); task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(); task.DueAt = dueAt;
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已更新任务：《{task.Title}》。" };
    }

    public async Task<OperationFeedback> DeleteTaskAsync(int taskId)
    {
        var profile = await GetCurrentProfileAsync();
        var task = await QueryTasksForProfile(profile).FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null) return new OperationFeedback { Succeeded = false, Message = "该任务不存在。" };
        dbContext.Tasks.Remove(task); await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已删除任务：《{task.Title}》。" };
    }

    public async Task<OperationFeedback> CompleteTaskAsync(int taskId)
    {
        var profile = await GetCurrentProfileAsync();
        var task = await QueryTasksForProfile(profile).FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || task.IsCompleted) return new OperationFeedback { Succeeded = false, Message = "这个任务已经处理过了。" };
        var spirit = await GetSelectedSpiritAsync(profile);
        if (spirit is null) return new OperationFeedback { Succeeded = false, Message = "当前没有可用精灵，请重新选择一位精灵伙伴。" };
        var beforeMood = profile.Mood; var beforeAffinity = profile.Affinity; var beforeCoins = profile.Coins; var beforeLevel = profile.Level;
        task.IsCompleted = true; task.CompletedAt = DateTime.Now;
        profile.Mood = Math.Min(100, profile.Mood + 5);
        profile.Affinity += 3 + spirit.TaskAffinityBonus;
        profile.Coins += 5;
        profile.Level = Math.Max(1, profile.Affinity / 100 + 1);
        profile.UpdatedAt = DateTime.Now;
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "spirit", SpiritId = spirit.Id, Content = $"{spirit.Name} 记录了这次完成。{spirit.SpecialMechanism}" });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"已完成任务：《{task.Title}》。", MoodDelta = profile.Mood - beforeMood, AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    // --- 每日互动、猜拳、演示重置 ---

    public async Task<OperationFeedback> PerformActionAsync(string actionType)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var profile = await GetCurrentProfileAsync();
        var log = await QueryDailyActionLogsForProfile(profile).FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == actionType)
                  ?? new DailyActionLog { UserProfileId = profile.Id, ActionDate = today, ActionType = actionType, Count = 0 };
        var spirit = await GetSelectedSpiritAsync(profile);
        if (spirit is null) return new OperationFeedback { Succeeded = false, Message = "当前没有可用精灵，请重新选择一位精灵伙伴。" };
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
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "spirit", SpiritId = spirit.Id, Content = personaService.BuildInteractionReply(spirit, actionType) });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = $"{GetActionDisplayName(actionType)}完成。", MoodDelta = profile.Mood - beforeMood, AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    public async Task<OperationFeedback> PlayGameAsync(string userChoice)
    {
        if (string.IsNullOrWhiteSpace(userChoice)) return new OperationFeedback { Succeeded = false, Message = "请先选择石头、布或剪刀。" };
        var profile = await GetCurrentProfileAsync();
        var spirit = await GetSelectedSpiritAsync(profile);
        if (spirit is null) return new OperationFeedback { Succeeded = false, Message = "当前没有可用精灵，请重新选择一位精灵伙伴。" };
        var beforeAffinity = profile.Affinity; var beforeCoins = profile.Coins; var beforeLevel = profile.Level;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var log = await QueryDailyActionLogsForProfile(profile).FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == "game")
                  ?? new DailyActionLog { UserProfileId = profile.Id, ActionDate = today, ActionType = "game", Count = 0 };
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
        dbContext.ChatMessages.Add(new ChatMessage { UserProfileId = profile.Id, Sender = "spirit", SpiritId = spirit.Id, Content = $"{spirit.Name} 出了 {spiritText}，你出了 {userText}。{personaService.BuildGameReply(spirit, outcome)}" });
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = outcome == "win" ? $"你赢了，{spirit.Name} 出了 {spiritText}。" : outcome == "draw" ? $"平局，{spirit.Name} 也出了 {spiritText}。" : $"{spirit.Name} 小胜，出了 {spiritText}。", AffinityDelta = profile.Affinity - beforeAffinity, CoinsDelta = profile.Coins - beforeCoins, LevelDelta = profile.Level - beforeLevel };
    }

    public async Task<OperationFeedback> ResetDemoDataAsync()
    {
        await EnsureInitializedAsync();
        var profile = await GetCurrentProfileAsync();
        dbContext.Tasks.RemoveRange(await QueryTasksForProfile(profile).ToListAsync());
        dbContext.ChatMessages.RemoveRange(await QueryChatMessagesForProfile(profile).ToListAsync());
        dbContext.DailyActionLogs.RemoveRange(await QueryDailyActionLogsForProfile(profile).ToListAsync());
        profile.Mood = 76; profile.Affinity = 18; profile.Level = 1; profile.Coins = 30; profile.UpdatedAt = DateTime.Now;
        dbContext.Tasks.AddRange(BuildDemoTasks(profile.Id)); dbContext.ChatMessages.AddRange(BuildDemoChatMessages(profile.Id, profile.CurrentSpiritId));
        await dbContext.SaveChangesAsync();
        return new OperationFeedback { Succeeded = true, Message = "演示数据已重置。" };
    }

    public async Task<OperationFeedback?> ApplyWelcomeBackEffectAsync()
    {
        var profile = await GetCurrentProfileAsync();
        var spirit = await GetSelectedSpiritAsync(profile);
        if (spirit is null) return null;
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
        var columns = await ReadColumnNamesAsync(connection, "UserProfiles");
        await AddColumnIfMissingAsync(connection, columns, "UserProfiles", "LastSpiritSwitchAt", "TEXT NULL");
        await AddColumnIfMissingAsync(connection, columns, "UserProfiles", "AccountUsername", "TEXT NULL");
        await ExecuteNonQueryAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserProfiles_AccountUsername\" ON \"UserProfiles\" (\"AccountUsername\")");
    }

    private async Task EnsureTaskSchemaAsync()
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var columns = await ReadColumnNamesAsync(connection, "Tasks");
        await AddColumnIfMissingAsync(connection, columns, "Tasks", "UserProfileId", "INTEGER NULL");
        await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_Tasks_UserProfileId\" ON \"Tasks\" (\"UserProfileId\")");
    }

    private async Task EnsureChatMessageSchemaAsync()
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var columns = await ReadColumnNamesAsync(connection, "ChatMessages");
        await AddColumnIfMissingAsync(connection, columns, "ChatMessages", "SpiritId", "TEXT NULL");
        await AddColumnIfMissingAsync(connection, columns, "ChatMessages", "UserProfileId", "INTEGER NULL");
        await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_ChatMessages_UserProfileId\" ON \"ChatMessages\" (\"UserProfileId\")");
    }

    private async Task EnsureDailyActionLogSchemaAsync()
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var columns = await ReadColumnNamesAsync(connection, "DailyActionLogs");
        await AddColumnIfMissingAsync(connection, columns, "DailyActionLogs", "UserProfileId", "INTEGER NULL");
        await ExecuteNonQueryAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_DailyActionLogs_UserProfileId_ActionDate_ActionType\" ON \"DailyActionLogs\" (\"UserProfileId\", \"ActionDate\", \"ActionType\")");
    }

    private async Task<UserProfile> EnsureCurrentProfileAsync()
    {
        var accountUsername = GetCurrentAccountUsername();
        if (!string.IsNullOrWhiteSpace(accountUsername))
        {
            var accountProfile = await dbContext.UserProfiles.FirstOrDefaultAsync(x => x.AccountUsername == accountUsername);
            if (accountProfile is not null)
            {
                return accountProfile;
            }

            accountProfile = new UserProfile
            {
                AccountUsername = accountUsername,
                Nickname = httpContextAccessor.HttpContext?.User.Identity?.Name?.Trim() ?? accountUsername,
                CurrentSpiritId = string.Empty,
                Mood = 76,
                Affinity = 18,
                Level = 1,
                Coins = 30
            };
            dbContext.UserProfiles.Add(accountProfile);
            await dbContext.SaveChangesAsync();
            return accountProfile;
        }

        var fallbackProfile = await dbContext.UserProfiles
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(x => x.AccountUsername == null);
        if (fallbackProfile is not null)
        {
            return fallbackProfile;
        }

        fallbackProfile = new UserProfile { Nickname = string.Empty, CurrentSpiritId = string.Empty, Mood = 76, Affinity = 18, Level = 1, Coins = 30 };
        dbContext.UserProfiles.Add(fallbackProfile);
        await dbContext.SaveChangesAsync();
        return fallbackProfile;
    }

    private async Task<UserProfile> GetCurrentProfileAsync()
    {
        var accountUsername = GetCurrentAccountUsername();
        if (!string.IsNullOrWhiteSpace(accountUsername))
        {
            var accountProfile = await dbContext.UserProfiles.FirstOrDefaultAsync(x => x.AccountUsername == accountUsername);
            if (accountProfile is not null)
            {
                return accountProfile;
            }
        }

        return await EnsureCurrentProfileAsync();
    }

    private string? GetCurrentAccountUsername()
    {
        var userName = httpContextAccessor.HttpContext?.User.Identity?.Name;
        return string.IsNullOrWhiteSpace(userName)
            ? null
            : WebAccountAuth.NormalizeUsername(userName);
    }

    private IQueryable<TaskItem> QueryTasksForProfile(UserProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.AccountUsername)
            ? dbContext.Tasks.Where(x => x.UserProfileId == profile.Id || x.UserProfileId == null)
            : dbContext.Tasks.Where(x => x.UserProfileId == profile.Id);
    }

    private IQueryable<ChatMessage> QueryChatMessagesForProfile(UserProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.AccountUsername)
            ? dbContext.ChatMessages.Where(x => x.UserProfileId == profile.Id || x.UserProfileId == null)
            : dbContext.ChatMessages.Where(x => x.UserProfileId == profile.Id);
    }

    private IQueryable<DailyActionLog> QueryDailyActionLogsForProfile(UserProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.AccountUsername)
            ? dbContext.DailyActionLogs.Where(x => x.UserProfileId == profile.Id || x.UserProfileId == null)
            : dbContext.DailyActionLogs.Where(x => x.UserProfileId == profile.Id);
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(System.Data.Common.DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.Common.DbConnection connection,
        HashSet<string> columns,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (columns.Contains(columnName))
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition}");
        columns.Add(columnName);
    }

    private static async Task ExecuteNonQueryAsync(System.Data.Common.DbConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<SpiritDefinition?> GetSelectedSpiritAsync(UserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId))
        {
            return null;
        }

        var spirit = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == profile.CurrentSpiritId);
        if (spirit is not null)
        {
            return spirit;
        }

        profile.CurrentSpiritId = string.Empty;
        profile.LastSpiritSwitchAt = null;
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
        return null;
    }

    private async Task<SpiritDefinition?> ResolveConversationSpiritAsync(UserProfile profile, string? spiritId)
    {
        if (!string.IsNullOrWhiteSpace(spiritId))
        {
            var requestedSpirit = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == spiritId);
            if (requestedSpirit is not null)
            {
                return requestedSpirit;
            }
        }

        return await GetSelectedSpiritAsync(profile);
    }

    private async Task<List<ChatMessage>> GetConversationMessagesAsync(UserProfile profile, string spiritId, int? take = null)
    {
        var allMessages = await QueryChatMessagesForProfile(profile).OrderBy(x => x.CreatedAt).ToListAsync();
        var filtered = FilterConversationMessages(allMessages, spiritId);
        if (take.HasValue && filtered.Count > take.Value)
        {
            return filtered.TakeLast(take.Value).ToList();
        }

        return filtered;
    }

    private static List<ChatMessage> FilterConversationMessages(List<ChatMessage> allMessages, string spiritId)
    {
        var threadedMessages = allMessages
            .Where(x => string.Equals(x.SpiritId, spiritId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (threadedMessages.Count > 0)
        {
            return threadedMessages;
        }

        return allMessages
            .Where(x => string.IsNullOrWhiteSpace(x.SpiritId))
            .ToList();
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

    private static List<TaskItem> BuildDemoTasks(int userProfileId) =>
    [
        new TaskItem { UserProfileId = userProfileId, Title = "完成今天的课程复盘", Description = "整理课堂重点，写出 3 条核心结论。", DueAt = DateTime.Now.AddHours(3) },
        new TaskItem { UserProfileId = userProfileId, Title = "准备明天的答辩展示", Description = "检查首页、聊天、任务、互动和小游戏流程。", DueAt = DateTime.Now.AddHours(8) },
        new TaskItem { UserProfileId = userProfileId, Title = "给自己留 20 分钟休息", Description = "起身活动、喝水、放松眼睛。", DueAt = DateTime.Now.AddDays(1) }
    ];

    private static List<ChatMessage> BuildDemoChatMessages(int userProfileId, string? spiritId) =>
    [
        new ChatMessage { UserProfileId = userProfileId, Sender = "spirit", SpiritId = string.IsNullOrWhiteSpace(spiritId) ? SpiritIds.Light : spiritId, Content = "欢迎来到 SpiritDesk。今天想先安排任务，还是先聊聊状态？" },
        new ChatMessage { UserProfileId = userProfileId, Sender = "user", SpiritId = string.IsNullOrWhiteSpace(spiritId) ? SpiritIds.Light : spiritId, Content = "先帮我看看今天最重要的三件事。" },
        new ChatMessage { UserProfileId = userProfileId, Sender = "spirit", SpiritId = string.IsNullOrWhiteSpace(spiritId) ? SpiritIds.Light : spiritId, Content = "建议先处理最紧急的一项，再留出时间准备答辩展示。" }
    ];
}
