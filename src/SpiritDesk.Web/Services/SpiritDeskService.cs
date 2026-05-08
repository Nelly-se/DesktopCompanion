using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Data;
using SpiritDesk.Web.Models;

namespace SpiritDesk.Web.Services;

public class SpiritDeskService(
    SpiritDeskDbContext dbContext,
    SpiritPersonaService personaService)
{
    public async Task<SpiritDeskViewModel> BuildViewModelAsync()
    {
        await EnsureInitializedAsync();

        var profile = await dbContext.UserProfiles.OrderBy(x => x.Id).FirstAsync();
        var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
        var currentSpirit = spirits.First(x => x.Id == profile.CurrentSpiritId);
        var tasks = await dbContext.Tasks.OrderBy(x => x.IsCompleted).ThenBy(x => x.DueAt).Take(12).ToListAsync();
        var chatMessages = await dbContext.ChatMessages.OrderByDescending(x => x.CreatedAt).Take(16).ToListAsync();
        chatMessages.Reverse();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var dailyCounts = await dbContext.DailyActionLogs
            .Where(x => x.ActionDate == today)
            .ToDictionaryAsync(x => x.ActionType, x => x.Count);

        var taskSnapshot = new TaskSnapshotViewModel
        {
            PendingTasks = tasks.Where(x => !x.IsCompleted).Take(6).ToList(),
            CompletedTasks = tasks.Where(x => x.IsCompleted).Take(6).ToList(),
            TodayTasks = tasks.Where(x => !x.IsCompleted && (!x.DueAt.HasValue || x.DueAt.Value.Date <= DateTime.Today)).ToList()
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
            HelperTip = personaService.BuildHelperTip(currentSpirit)
        };
    }

    public async Task EnsureInitializedAsync()
    {
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.UserProfiles.AnyAsync())
        {
            dbContext.UserProfiles.Add(new UserProfile
            {
                Nickname = "zyx",
                CurrentSpiritId = string.Empty,
                Mood = 76,
                Affinity = 18,
                Level = 1,
                Coins = 30
            });
        }

        if (!await dbContext.Tasks.AnyAsync())
        {
            dbContext.Tasks.AddRange(
                new TaskItem
                {
                    Title = "完成精灵模块重构",
                    Description = "严格替换五个精灵名字与称号，并按 MBTI 规则调整激励机制",
                    DueAt = DateTime.Now.AddHours(4)
                },
                new TaskItem
                {
                    Title = "重写桌面三栏 UI",
                    Description = "按参考图调整左侧导航、中部主视图和右侧精灵助手面板",
                    DueAt = DateTime.Now.AddHours(12)
                },
                new TaskItem
                {
                    Title = "补充初始精灵选择页",
                    Description = "首次进入时展示五个精灵卡片和详细介绍",
                    DueAt = DateTime.Now.AddDays(1)
                });
        }

        if (!await dbContext.ChatMessages.AnyAsync())
        {
            dbContext.ChatMessages.AddRange(
                new ChatMessage { Sender = "spirit", Content = "欢迎来到 SpiritDesk，我会按照你的精灵性格设定来陪你完成今天。" },
                new ChatMessage { Sender = "user", Content = "今天先把桌面 UI 和精灵模块改好。" },
                new ChatMessage { Sender = "spirit", Content = "收到，我们先统一精灵设定，再把界面修到答辩可展示的程度。" });
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

    public async Task SelectSpiritAsync(string spiritId)
    {
        var profile = await dbContext.UserProfiles.FirstAsync();
        if (!await dbContext.Spirits.AnyAsync(x => x.Id == spiritId))
        {
            return;
        }

        profile.CurrentSpiritId = spiritId;
        profile.UpdatedAt = DateTime.Now;

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = $"已与你的精灵伴侣绑定：{(await dbContext.Spirits.FirstAsync(x => x.Id == spiritId)).Name}。"
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId))
        {
            return;
        }

        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "user",
            Content = message.Trim()
        });

        dbContext.ChatMessages.Add(new ChatMessage
        {
            Sender = "spirit",
            Content = personaService.GenerateReply(spirit, message)
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

        var profile = await dbContext.UserProfiles.FirstAsync();
        profile.Nickname = nickname.Trim();
        profile.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync();
    }

    public async Task AddTaskAsync(string title, string? description, DateTime? dueAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        dbContext.Tasks.Add(new TaskItem
        {
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueAt = dueAt
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task CompleteTaskAsync(int taskId)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(x => x.Id == taskId);
        if (task is null || task.IsCompleted)
        {
            return;
        }

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);

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
    }

    public async Task PerformActionAsync(string actionType)
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
            return;
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
                    return;
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
    }

    public async Task PlayGameAsync(string userChoice)
    {
        if (string.IsNullOrWhiteSpace(userChoice))
        {
            return;
        }

        var profile = await dbContext.UserProfiles.FirstAsync();
        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);

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
            return;
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
            Content = $"{spirit.Name} 出了{spiritText}，你出了{userText}。{personaService.BuildGameReply(spirit, outcome)}"
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task ApplyWelcomeBackEffectAsync()
    {
        var profile = await dbContext.UserProfiles.FirstAsync();
        if (string.IsNullOrWhiteSpace(profile.CurrentSpiritId))
        {
            return;
        }

        var spirit = await dbContext.Spirits.FirstAsync(x => x.Id == profile.CurrentSpiritId);
        if (spirit.WelcomeBackCompensation)
        {
            profile.Mood = Math.Min(100, profile.Mood + 10);
            profile.UpdatedAt = DateTime.Now;
            await dbContext.SaveChangesAsync();
        }
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
}
