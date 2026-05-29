# 03 - C# 讲解：数据层与 EF Core

## 1. SpiritDeskDbContext

文件：`Data/SpiritDeskDbContext.cs`

采用 C# 12 **主构造函数**注入 `DbContextOptions`：

```csharp
public class SpiritDeskDbContext(DbContextOptions<SpiritDeskDbContext> options) : DbContext(options)
```

### DbSet 一览

| DbSet | 实体 | 用途 |
|-------|------|------|
| `UserProfiles` | `UserProfile` | 用户档案，使用 `AccountUsername` 绑定登录账号 |
| `WebAccounts` | `WebAccount` | 登录账号 |
| `Spirits` | `SpiritDefinition` | 五精灵配置 |
| `Tasks` | `TaskItem` | 待办，使用 `UserProfileId` 隔离账号数据 |
| `ChatMessages` | `ChatMessage` | 聊天，使用 `UserProfileId` 隔离账号数据 |
| `DailyActionLogs` | `DailyActionLog` | 每日动作计数，使用 `UserProfileId` 隔离账号数据 |

## 2. OnModelCreating 配置

### WebAccount

- 主键 `Id`
- `Username` 唯一索引，最大长度 64
- `PasswordHash` 必填，最大 500

### UserProfile / 业务数据隔离

- `UserProfile.AccountUsername` 与 `WebAccount.Username` 使用同一套规范化规则。
- `TaskItem.UserProfileId`、`ChatMessage.UserProfileId`、`DailyActionLog.UserProfileId` 指向当前档案。
- `DailyActionLog` 使用 `UserProfileId + ActionDate + ActionType` 唯一索引，保证同一账号同一天同一动作只累计一行。

### DailyActionLog

`ActionDate` 使用 `DateOnly`，在 SQLite 中通过值转换存为 `DateTime`：

```csharp
entity.Property(x => x.ActionDate).HasConversion(
    value => value.ToDateTime(TimeOnly.MinValue),
    value => DateOnly.FromDateTime(value));
```

### SpiritDefinition 种子数据

`modelBuilder.Entity<SpiritDefinition>().HasData(...)` 写入五条精灵（卷卷晴、嘻嘻滴、贴贴朵、慢慢壤、新新星），包含 `AccentColor`、`ImagePath`、各加成字段。

首次 `EnsureCreated` 或迁移后会存在这些行；业务层 `EnsureSpiritDefinitionsAsync` 负责与代码内定义同步更新文本字段。

## 3. 数据库文件路径

在 `SpiritDeskWebHost.Build` 中：

```csharp
var databasePath = Path.Combine(dataDirectory, "spiritdesk.db");
```

连接串默认：

```text
Data Source={databasePath}
```

可通过 `appsettings.json` 的 `ConnectionStrings:SpiritDesk` 覆盖。

## 4. DatabaseBootstrapper

`Helpers/DatabaseBootstrapper.cs` 在应用启动时对已有 SQLite 文件做**兼容性检查**（`PRAGMA table_info` 核对必需列）。若表结构不兼容，会**备份旧库并删除**，再由后续 `EnsureCreated` 重建；`WebAccounts` 表则通过 `CREATE TABLE IF NOT EXISTS` 幂等补建。少量增量列由 `SpiritDeskService` 内手写 `ALTER TABLE` 完成。详见 [basic/14-数据库文件与迁移详解.md](./basic/14-数据库文件与迁移详解.md)。

## 5. 初始化逻辑（业务层配合）

`SpiritDeskService.EnsureInitializedAsync`：

```csharp
await dbContext.Database.EnsureCreatedAsync();
await EnsureUserProfileSchemaAsync();
await EnsureTaskSchemaAsync();
await EnsureChatMessageSchemaAsync();
await EnsureDailyActionLogSchemaAsync();
await EnsureSpiritDefinitionsAsync();

var profile = await EnsureCurrentProfileAsync();

if (!await QueryTasksForProfile(profile).AnyAsync())
    dbContext.Tasks.AddRange(BuildDemoTasks(profile.Id));

if (!await QueryChatMessagesForProfile(profile).AnyAsync())
    dbContext.ChatMessages.AddRange(BuildDemoChatMessages(profile.Id, profile.CurrentSpiritId));

await dbContext.SaveChangesAsync();
```

说明：

- 使用 `EnsureCreatedAsync` 而非完整 Migrations 工作流，适合课程/演示快速部署（**注意**：`EnsureCreated` 不会给已有库自动加列；本项目用 Bootstrapper + Service 内补列弥补）。
- 当前账号首次进入时创建或读取自己的 `UserProfile`，并按该档案插入演示任务与聊天，保证首页非空。
- 若需 TypeORM 式「带时间戳 migration 进 Git、保留用户数据升级」，应改为 `dotnet ef migrations` + `Migrate()`，见 [basic/14-数据库文件与迁移详解.md](./basic/14-数据库文件与迁移详解.md)。

## 6. 典型查询模式

**加载工作台**（`BuildViewModelAsync`）：

```csharp
var profile = await GetCurrentProfileAsync();
var spirits = await dbContext.Spirits.OrderBy(x => x.Id).ToListAsync();
var currentSpirit = spirits.First(x => x.Id == profile.CurrentSpiritId);
var tasks = await QueryTasksForProfile(profile).OrderBy(...).Take(200).ToListAsync();
```

**切换精灵**：

```csharp
profile.CurrentSpiritId = spiritId;
dbContext.ChatMessages.Add(new ChatMessage { Sender = "spirit", Content = "..." });
await dbContext.SaveChangesAsync();
```

## 7. UserProfile 与 WebAccount 的关系

| 表 | 场景 |
|----|------|
| `WebAccounts` | 登录 / 注册校验 |
| `UserProfiles` | 账号绑定的游戏进度、当前精灵、昵称和数值 |
| `Tasks` / `ChatMessages` / `DailyActionLogs` | 通过 `UserProfileId` 归属到具体档案 |

当前实现已经按登录账号区分档案：注册时写入 `WebAccounts` 并创建同名 `UserProfile`；登录后 `SpiritDeskService` 根据 Cookie 用户名找到当前档案，再过滤任务、聊天与每日互动数据。

## 8. 学习建议

1. 用 **DB Browser for SQLite** 打开 `data/spiritdesk.db` 对照表结构。
2. 修改 `SpiritDefinition` 种子后，若库已存在，需删库或依赖 `EnsureSpiritDefinitionsAsync` 更新。
3. 理解 **Scoped DbContext**：每个 HTTP 请求一个上下文，PageModel 与 Service 同请求内共享。

下一步阅读：[04-CSharp-业务服务层.md](./04-CSharp-业务服务层.md)
