# 01 - C# 讲解：SpiritDesk.Core

## 1. 项目定位

`SpiritDesk.Core` 是 **.NET 9 类库**（`SpiritDesk.Core.csproj`），不引用 ASP.NET、EF、WPF。其作用是把「精灵、用户、任务、聊天」等**领域对象**和**常量**集中在一处，供 `SpiritDesk.Web` 引用。

这样 Web 层的 `DbContext`、Service 与 Razor 页面都使用同一套类型定义，避免重复声明。

## 2. SpiritIds 常量

文件：`Constants/SpiritIds.cs`

```csharp
public static class SpiritIds
{
    public const string Air = "air";
    public const string Light = "light";
    public const string Nutrition = "nutrition";
    public const string Soil = "soil";
    public const string Water = "water";
}
```

五精灵在数据库中的主键即上述字符串。`SpiritDeskDbContext` 种子数据、`SelectSpiritAsync`、浮球右键切换等均使用这些 ID。

## 3. 实体类说明

### 3.1 UserProfile

表示**单机演示场景下的唯一用户档案**（非多用户表设计）。

| 属性 | 含义 |
|------|------|
| `Nickname` | 用户昵称 |
| `CurrentSpiritId` | 当前绑定精灵 ID，空表示尚未五选一 |
| `Mood` / `Affinity` / `Level` / `Coins` | 成长数值 |
| `LastSpiritSwitchAt` | 上次切换精灵时间 |
| `CreatedAt` / `UpdatedAt` | 时间戳 |

首次启动时 `SpiritDeskService.EnsureInitializedAsync` 会插入一条空档案，引导用户去 `Spirits/Select` 选精灵。

### 3.2 SpiritDefinition

精灵静态配置，字段包括：

- 展示：`Name`、`Title`、`Mbti`、`Personality`、`Description`、`DialogueExample`、`ImagePath`、`AccentColor`
- 机制：`TaskAffinityBonus`、`FeedMoodBonus`、`CheckInBonusMultiplier`、`GameCountBonus` 等

数据库通过 `HasData` 种子写入五条记录；`SpiritDeskService.EnsureSpiritDefinitionsAsync` 也会在运行时与代码内 `BuildSpiritDefinitions()` 对齐更新。

### 3.3 TaskItem

待办任务：`Title`、`Description`、`DueAt`、`IsCompleted` 等，供首页日历与任务管理使用。

### 3.4 ChatMessage

聊天记录：`Sender` 为 `"user"` 或 `"spirit"`，`Content` 为文本，`CreatedAt` 为时间。

### 3.5 DailyActionLog

按 `ActionDate`（`DateOnly`）与 `ActionType`（如 checkin、feed）记录每日次数，用于限制小游戏次数、展示今日互动统计。

### 3.6 WebAccount

Web 注册账号：`Username`（唯一）、`PasswordHash`（由 `IPasswordHasher<WebAccount>` 生成）。与 `UserProfile` 分离：**认证账号**与**游戏档案**是两套概念。

## 4. 在解决方案中的引用方式

`SpiritDesk.Web.csproj`：

```xml
<ProjectReference Include="..\SpiritDesk.Core\SpiritDesk.Core.csproj" />
```

典型 using：

```csharp
using SpiritDesk.Core.Entities;
using SpiritDesk.Core.Constants;
```

## 5. 学习要点

1. **类库无启动入口**：不能 `dotnet run SpiritDesk.Core`，只能被其他项目引用。
2. **POCO 实体**：普通属性 + 自动属性，无行为方法；业务逻辑在 Web 的 `Services` 中。
3. **ID 用 string 常量**：便于种子数据、API JSON、配置统一，比枚举更利于 EF 种子与前端传参一致。

下一步阅读：[02-CSharp-Web启动与SpiritDeskWebHost.md](./02-CSharp-Web启动与SpiritDeskWebHost.md)
