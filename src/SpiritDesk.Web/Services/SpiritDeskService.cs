using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Data;
using SpiritDesk.Web.Models;

namespace SpiritDesk.Web.Services;

public class SpiritDeskService(
    SpiritDeskDbContext dbContext,
    SpiritPersonaService personaService,
    LlmReplyService llmReplyService)
{
    private static readonly HashSet<string> LegacyTaskTitles =
    [
        "完成精灵模块重构",
        "重写桌面三栏 UI",
        "补充初始精灵选择页"
    ];

    private static readonly string[] LegacyChatMessages =
    [
        "欢迎来到 SpiritDesk，我会按照你的精灵性格设定来陪你完成今天。",
        "今天先把桌面 UI 和精灵模块改好。",
        "收到，我们先统一精灵设定，再把界面修到答辩可展示的程度。"
    ];

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
        var dailyCounts = await dbContext.DailyActionLogs
            .Where(x => x.ActionDate == today)
            .ToDictionaryAsync(x => x.ActionType, x => x.Count);

        var taskSnapshot = new TaskSnapshotViewModel
        {
            PendingTasks = tasks.Where(x => !x.IsCompleted).Take(8).ToList(),
            CompletedTasks = tasks.Where(x => x.IsCompleted).Take(8).ToList(),
            TodayTasks = tasks.Where(x => !x.IsCompleted && (!x.DueAt.HasValue || x.DueAt.Value.Date <= DateTime.Today)).ToList(),
            DueReminders = tasks
                .Where(x => !x.IsCompleted && x.DueAt.HasValue && x.DueAt.Value <= DateTime.Now.AddHours(24))
                .OrderBy(x => x.DueAt)
                .Take(4)
                .ToList()
        };

        return new SpiritDeskViewModel
        {
            Profile = profile,
            CurrentSpirit = currentSpirit,
            Spirits = spirits,
            TaskSnapshot = taskSnapshot,
            Tasks = tasks,
            ChatMessages = chatMessages,
            DailyCounts = dailyCounts,
            Greeting = personaService.BuildGreeting(currentSpirit, profile.Nickname),
            WelcomeBackMessage = personaService.BuildWelcomeBack(currentSpirit),
            HelperTip = personaService.BuildHelperTip(currentSpirit),
            ReturnNotice = await BuildReturnNoticeAsync()
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
            CooldownMessage = "当前演示版允许随时切换精灵，切换后立即生效，便于答辩展示五种人设差异。",
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
            dbContext.UserProfiles.Add(new UserProfile
            {
                Nickname = string.Empty,
                CurrentSpiritId = string.Empty,
                Mood = 76,
                Affinity = 18,
                Level = 1,
                Coins = 30
            });
        }

        if (!await dbContext.Tasks.AnyAsync())
        {
            dbContext.Tasks.AddRange(BuildDemoTasks());
        }
        else
        {
            await RefreshLegacyDemoTasksAsync();
        }

        if (!await dbContext.ChatMessages.AnyAsync())
        {
            dbContext.ChatMessages.AddRange(BuildDemoChatMessages());
        }
        else
        {
            await RefreshLegacyDemoChatMessagesAsync();
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> NeedsSpiritSelectionAsync()
    {
        await EnsureInitializedAsync();
        var profile = await dbContext.UserProfiles.FirstAsync();
        return string.IsNullOrWhiteSpace(profile.CurrentSpiritId);
    }

    public async Task<List<SpiritDefinition>> GetSpiritsAsync()
    {
        await EnsureInitializedAsync();
        return await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
    }

    public async Task<SpiritSelectionResult> SelectSpiritAsync(string spiritId)
    {
        await EnsureInitializedAsync();

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == spiritId);
        if (spirit is null)
        {
            return new SpiritSelectionResult
            {
                Succeeded = false,
                Message = "未找到该精灵。"
            };
        }

        profile.CurrentSpiritId = spiritId;
        profile.LastSpiritSwitchAt = DateTime.Now;
        profile.UpdatedAt = DateTime.Now;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = $"已与你的精灵伙伴绑定：{spirit.Name}。"
        });

        await dbContext.SaveChangesAsync();
        return new SpiritSelectionResult
        {
            Succeeded = true,
            Message = $"已切换为 {spirit.Name}。",
            SpiritName = spirit.Name
        };
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        await EnsureInitializedAsync();

        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId))
        {
            return;
        }

        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var trimmed = message.Trim();

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "user",
            Content = trimmed
        });

        var fallbackReply = personaService.GenerateReply(spirit, trimmed);
        var reply = await llmReplyService.GenerateReplyAsync(spirit, profile.Nickname, trimmed, fallbackReply);

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = reply
        });

        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task RenameAsync(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return;
        }

        await EnsureInitializedAsync();

        var profile = await dbContext.UserProfiles.FirstAsync();
        profile.Nickname = nickname.Trim();
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task<OperationFeedback> AddTaskAsync(string title, string? description, DateTime? dueAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "请输入任务标题后再创建任务。"
            };
        }

        var normalizedTitle = title.Trim();
        dbContext.Tasks.Add(new TaskItem
        {
            Title = normalizedTitle,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueAt = dueAt
        });

        await dbContext.SaveChangesAsync();
        return new OperationFeedback
        {
            Succeeded = true,
            Message = $"已添加任务《{normalizedTitle}》。"
        };
    }

    public async Task<OperationFeedback> UpdateTaskAsync(int taskId, string title, string? description, DateTime? dueAt)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null)
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "未找到要编辑的任务。"
            };
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "编辑任务时标题不能为空。"
            };
        }

        task.Title = title.Trim();
        task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        task.DueAt = dueAt;

        await dbContext.SaveChangesAsync();
        return new OperationFeedback
        {
            Succeeded = true,
            Message = $"已更新任务《{task.Title}》。"
        };
    }

    public async Task<OperationFeedback> DeleteTaskAsync(int taskId)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null)
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "该任务已不存在。"
            };
        }

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync();

        return new OperationFeedback
        {
            Succeeded = true,
            Message = $"已删除任务《{task.Title}》。"
        };
    }

    public async Task<OperationFeedback> CompleteTaskAsync(int taskId)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || task.IsCompleted)
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "这个任务已经处理过了，列表已为你保持最新状态。"
            };
        }

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeMood = profile.Mood;
        var beforeAffinity = profile.Affinity;
        var beforeCoins = profile.Coins;
        var beforeLevel = profile.Level;

        task.IsCompleted = true;
        task.CompletedAt = DateTime.Now;

        profile.Mood = Math.Min(100, profile.Mood + 5);
        profile.Affinity += 3 + spirit.TaskAffinityBonus;
        profile.Coins += 5;
        profile.Level = CalculateLevel(profile.Affinity);
        profile.UpdatedAt = DateTime.Now;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = $"{spirit.Name} 记录了这次完成。{spirit.SpecialMechanism}"
        });

        await dbContext.SaveChangesAsync();
        return new OperationFeedback
        {
            Succeeded = true,
            Message = $"已完成任务《{task.Title}》。",
            MoodDelta = profile.Mood - beforeMood,
            AffinityDelta = profile.Affinity - beforeAffinity,
            CoinsDelta = profile.Coins - beforeCoins,
            LevelDelta = profile.Level - beforeLevel
        };
    }

    public async Task<OperationFeedback> PerformActionAsync(string actionType)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var log = await dbContext.DailyActionLogs.FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == actionType);
        log ??= new DailyActionLog
        {
            ActionDate = today,
            ActionType = actionType,
            Count = 0
        };

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeMood = profile.Mood;
        var beforeAffinity = profile.Affinity;
        var beforeCoins = profile.Coins;
        var beforeLevel = profile.Level;

        var limits = new Dictionary<string, int>
        {
            ["checkin"] = 1,
            ["feed"] = 3,
            ["encourage"] = 5,
            ["study"] = 5,
            ["rest"] = 5,
            ["game"] = 5 + spirit.GameCountBonus
        };

        if (limits.TryGetValue(actionType, out var limit) && log.Count >= limit)
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = $"{GetActionDisplayName(actionType)}已达到今日上限。"
            };
        }

        if (log.Id == 0)
        {
            dbContext.DailyActionLogs.Add(log);
        }

        log.Count += 1;

        switch (actionType)
        {
            case "checkin":
                profile.Mood = Math.Min(100, profile.Mood + 5 * spirit.CheckInBonusMultiplier);
                profile.Affinity += 5 * spirit.CheckInBonusMultiplier;
                profile.Coins += 10 * spirit.CheckInBonusMultiplier;
                break;
            case "feed":
                if (profile.Coins < 5)
                {
                    return new OperationFeedback
                    {
                        Succeeded = false,
                        Message = "金币不足，投喂精灵至少需要 5 金币。"
                    };
                }

                profile.Coins -= 5;
                profile.Mood = Math.Min(100, profile.Mood + 10 + spirit.FeedMoodBonus);
                profile.Affinity += 2;
                break;
            case "encourage":
            case "study":
            case "rest":
                profile.Mood = Math.Min(100, profile.Mood + 2);
                profile.Affinity += 1;
                break;
        }

        profile.Level = CalculateLevel(profile.Affinity);
        profile.UpdatedAt = DateTime.Now;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = personaService.BuildInteractionReply(spirit, actionType)
        });

        await dbContext.SaveChangesAsync();
        return new OperationFeedback
        {
            Succeeded = true,
            Message = $"{GetActionDisplayName(actionType)}完成。",
            MoodDelta = profile.Mood - beforeMood,
            AffinityDelta = profile.Affinity - beforeAffinity,
            CoinsDelta = profile.Coins - beforeCoins,
            LevelDelta = profile.Level - beforeLevel
        };
    }

    public async Task<OperationFeedback> PlayGameAsync(string userChoice)
    {
        if (string.IsNullOrWhiteSpace(userChoice))
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "请先选择石头、布或剪刀。"
            };
        }

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        var beforeAffinity = profile.Affinity;
        var beforeCoins = profile.Coins;
        var beforeLevel = profile.Level;

        var today = DateOnly.FromDateTime(DateTime.Now);
        var log = await dbContext.DailyActionLogs.FirstOrDefaultAsync(x => x.ActionDate == today && x.ActionType == "game");
        log ??= new DailyActionLog
        {
            ActionDate = today,
            ActionType = "game",
            Count = 0
        };

        var maxGameCount = 5 + spirit.GameCountBonus;
        if (log.Count >= maxGameCount)
        {
            return new OperationFeedback
            {
                Succeeded = false,
                Message = "今天的小游戏次数已经用完了，明天再来一局吧。"
            };
        }

        if (log.Id == 0)
        {
            dbContext.DailyActionLogs.Add(log);
        }

        log.Count += 1;

        var choices = new[] { "rock", "paper", "scissors" };
        var spiritChoice = choices[Random.Shared.Next(choices.Length)];
        var outcome = ResolveOutcome(userChoice, spiritChoice);

        profile.Affinity += outcome switch
        {
            "win" => 5,
            "draw" => 2,
            _ => 1
        };
        profile.Coins += outcome switch
        {
            "win" => 5 + spirit.GameCoinBonus,
            "draw" => 2 + spirit.GameCoinBonus,
            _ => spirit.GameCoinBonus
        };
        profile.Level = CalculateLevel(profile.Affinity);
        profile.UpdatedAt = DateTime.Now;

        var userText = ToChoiceText(userChoice);
        var spiritText = ToChoiceText(spiritChoice);
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = $"{spirit.Name} 出了 {spiritText}，你出了 {userText}。{personaService.BuildGameReply(spirit, outcome)}"
        });

        await dbContext.SaveChangesAsync();
        return new OperationFeedback
        {
            Succeeded = true,
            Message = outcome switch
            {
                "win" => $"你这局赢了，{spirit.Name} 出了 {spiritText}。",
                "draw" => $"这局平手，{spirit.Name} 出了 {spiritText}。",
                _ => $"{spirit.Name} 这局小胜，出了 {spiritText}。"
            },
            AffinityDelta = profile.Affinity - beforeAffinity,
            CoinsDelta = profile.Coins - beforeCoins,
            LevelDelta = profile.Level - beforeLevel
        };
    }

    public async Task<OperationFeedback> ResetDemoDataAsync()
    {
        await EnsureInitializedAsync();

        var tasks = await dbContext.Tasks.ToListAsync();
        dbContext.Tasks.RemoveRange(tasks);

        var messages = await dbContext.ChatMessages.ToListAsync();
        dbContext.ChatMessages.RemoveRange(messages);

        var logs = await dbContext.DailyActionLogs.ToListAsync();
        dbContext.DailyActionLogs.RemoveRange(logs);

        var profile = await dbContext.UserProfiles.FirstAsync();
        profile.Mood = 76;
        profile.Affinity = 18;
        profile.Level = 1;
        profile.Coins = 30;
        profile.UpdatedAt = DateTime.Now;

        dbContext.Tasks.AddRange(BuildDemoTasks());
        dbContext.ChatMessages.AddRange(BuildDemoChatMessages());

        await dbContext.SaveChangesAsync();

        return new OperationFeedback
        {
            Succeeded = true,
            Message = "演示数据已重置：任务、对话与今日互动计数已恢复为初始示例状态。"
        };
    }

    public async Task<OperationFeedback?> ApplyWelcomeBackEffectAsync()
    {
        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId))
        {
            return null;
        }

        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        if (!spirit.WelcomeBackCompensation)
        {
            return null;
        }

        var lastActiveAt = profile.UpdatedAt;
        if ((DateTime.Now - lastActiveAt) < TimeSpan.FromHours(6))
        {
            return null;
        }

        var beforeMood = profile.Mood;
        profile.Mood = Math.Min(100, profile.Mood + 10);
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();

        return new OperationFeedback
        {
            Succeeded = true,
            Message = "慢慢壤发现你隔了一段时间才回来，已经为你补上一点心情值。",
            MoodDelta = profile.Mood - beforeMood
        };
    }

    private async Task EnsureSpiritDefinitionsAsync()
    {
        var definitions = BuildSpiritDefinitions();

        foreach (var definition in definitions)
        {
            var existing = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == definition.Id);
            if (existing is null)
            {
                dbContext.Spirits.Add(definition);
                continue;
            }

            existing.Name = definition.Name;
            existing.Mbti = definition.Mbti;
            existing.Title = definition.Title;
            existing.CoreRole = definition.CoreRole;
            existing.ElementType = definition.ElementType;
            existing.Personality = definition.Personality;
            existing.Description = definition.Description;
            existing.DialogueExample = definition.DialogueExample;
            existing.SpecialMechanism = definition.SpecialMechanism;
            existing.ImagePath = definition.ImagePath;
            existing.AccentColor = definition.AccentColor;
            existing.CheckInBonusMultiplier = definition.CheckInBonusMultiplier;
            existing.TaskAffinityBonus = definition.TaskAffinityBonus;
            existing.FeedMoodBonus = definition.FeedMoodBonus;
            existing.GameCountBonus = definition.GameCountBonus;
            existing.GameCoinBonus = definition.GameCoinBonus;
            existing.MoodDecayReduction = definition.MoodDecayReduction;
            existing.WelcomeBackCompensation = definition.WelcomeBackCompensation;
        }
    }

    private async Task RefreshLegacyDemoTasksAsync()
    {
        var tasks = await dbContext.Tasks.OrderBy(x => x.Id).ToListAsync();
        if (tasks.Count != 3 || tasks.Any(x => !LegacyTaskTitles.Contains(x.Title)))
        {
            return;
        }

        dbContext.Tasks.RemoveRange(tasks);
        dbContext.Tasks.AddRange(BuildDemoTasks());
    }

    private async Task RefreshLegacyDemoChatMessagesAsync()
    {
        var messages = await dbContext.ChatMessages.OrderBy(x => x.Id).ToListAsync();
        if (messages.Count != 3 || messages.Select(x => x.Content).Except(LegacyChatMessages).Any())
        {
            return;
        }

        dbContext.ChatMessages.RemoveRange(messages);
        dbContext.ChatMessages.AddRange(BuildDemoChatMessages());
    }

    private async Task EnsureUserProfileSchemaAsync()
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"UserProfiles\")";
        await using var reader = await command.ExecuteReaderAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("LastSpiritSwitchAt"))
        {
            await using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE \"UserProfiles\" ADD COLUMN \"LastSpiritSwitchAt\" TEXT NULL";
            await alter.ExecuteNonQueryAsync();
        }
    }

    private async Task<string?> BuildReturnNoticeAsync()
    {
        var feedback = await ApplyWelcomeBackEffectAsync();
        return feedback?.ToNoticeMessage();
    }

    private static int CalculateLevel(int affinity) => Math.Max(1, affinity / 100 + 1);

    private static string ResolveOutcome(string userChoice, string spiritChoice)
    {
        if (userChoice == spiritChoice)
        {
            return "draw";
        }

        return (userChoice, spiritChoice) switch
        {
            ("rock", "scissors") => "win",
            ("paper", "rock") => "win",
            ("scissors", "paper") => "win",
            _ => "lose"
        };
    }

    private static string ToChoiceText(string choice) => choice switch
    {
        "rock" => "石头",
        "paper" => "布",
        "scissors" => "剪刀",
        _ => choice
    };

    private static string GetActionDisplayName(string actionType) => actionType switch
    {
        "checkin" => "每日签到",
        "feed" => "投喂精灵",
        "encourage" => "夸夸自己",
        "study" => "陪我学习",
        "rest" => "提醒休息",
        "game" => "小游戏",
        _ => "互动操作"
    };

    private static List<SpiritDefinition> BuildSpiritDefinitions()
    {
        return
        [
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
                SpecialMechanism = "完成任务时亲密度额外 +2；效率类问题回答更详细。",
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
                Description = "擅长用快乐感染他人，总能找到生活中好玩的事情，是气氛担当。",
                DialogueExample = "哇！今天有什么开心的事分享给我嘛？不开心？来，跟我一起做个鬼脸，哈哈！",
                SpecialMechanism = "互动和投喂时心情值额外 +3；永远优先给予正向情绪反馈。",
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
                Personality = "温暖贴心，善于倾听，重视人际关系。",
                Description = "会提醒用户关心朋友、家人，促进真实社交联结。",
                DialogueExample = "你好像很久没联系好朋友了，要不要发个消息问候一下？在人际关系中，偶尔示弱也是一种勇敢哦。",
                SpecialMechanism = "签到奖励翻倍；提供友善型社交建议。",
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
                Personality = "温和细腻，敏感而富有同理心。",
                Description = "关注用户的身心健康，鼓励休息、放慢节奏、接纳情绪。",
                DialogueExample = "累了就休息一下吧，今天的你已经很棒了。记得喝水哦，身体是你最忠实的朋友。",
                SpecialMechanism = "长时间未登录后返回，不降心情且额外补偿；压力型提问优先安抚。",
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
                Description = "鼓励用户尝试新爱好、拓展知识边界、保持创造力。",
                DialogueExample = "我最近发现了一个超酷的知识点，想知道吗？要不要试试用不同的方式完成今天的任务？",
                SpecialMechanism = "小游戏次数 +2；对“为什么”类问题的回答更具思辨性。",
                ImagePath = "/assets/images/spirit-nutrition.png",
                AccentColor = "#FF9A73",
                GameCountBonus = 2,
                GameCoinBonus = 1
            }
        ];
    }

    private static List<TaskItem> BuildDemoTasks()
    {
        return
        [
            new TaskItem
            {
                Title = "完成今天的课程复盘",
                Description = "整理课堂重点，写出 3 条最值得带走的结论。",
                DueAt = DateTime.Now.AddHours(3)
            },
            new TaskItem
            {
                Title = "准备明天的答辩展示",
                Description = "确认演示顺序，检查首页、聊天、任务和小游戏流程。",
                DueAt = DateTime.Now.AddHours(8)
            },
            new TaskItem
            {
                Title = "给自己留 20 分钟休息时间",
                Description = "起身活动、喝水、放松眼睛，避免后面状态掉线。",
                DueAt = DateTime.Now.AddDays(1)
            }
        ];
    }

    private static List<ChatMessage> BuildDemoChatMessages()
    {
        return
        [
            new ChatMessage { Sender = "spirit", Content = "欢迎来到 SpiritDesk。今天想先安排任务，还是先聊聊你的状态？" },
            new ChatMessage { Sender = "user", Content = "先帮我看看今天最重要的三件事。" },
            new ChatMessage { Sender = "spirit", Content = "没问题，我建议先处理最紧急的一项，再留一点体力给后面的展示准备。" }
        ];
    }
}
