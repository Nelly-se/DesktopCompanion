# 02 - spiritdesk.db 是怎么创建出来的？

> 这篇回答核心问题：  
> **本项目的数据库文件到底是怎么从无到有创建出来的？**

---

## 0. 一句话

`spiritdesk.db` 不是手动创建的，而是在程序运行时由 EF Core 自动创建。

完整链路：

```text
启动 Web 宿主
  ↓
确定 data 目录和 spiritdesk.db 路径
  ↓
注册 SpiritDeskDbContext 使用 SQLite
  ↓
页面 / API 调用 SpiritDeskService
  ↓
EnsureInitializedAsync()
  ↓
dbContext.Database.EnsureCreatedAsync()
  ↓
EF Core 根据 Entity + OnModelCreating 创建 SQLite 文件和数据表
```

---

## 1. 第一步：程序启动 Web 宿主

本项目 Web 启动入口会进入：

```text
src/SpiritDesk.Web/SpiritDeskWebHost.cs
```

里面的核心方法是：

```csharp
public static WebApplication Build(...)
```

它负责组装整个 Web 应用：

- 配置 Razor Pages
- 配置 Cookie 登录
- 配置静态文件
- 配置 API
- 配置 EF Core
- 配置 SQLite 数据库

所以数据库创建流程的第一站是 `SpiritDeskWebHost.cs`。

---

## 2. 第二步：确定 data 目录

代码：

```csharp
var dataDirectory = builder.Configuration["SpiritDesk:DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDirectory))
{
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
}

Directory.CreateDirectory(dataDirectory);
```

含义：

1. 先从配置里读取 `SpiritDesk:DataDirectory`。
2. 如果配置为空，就使用程序运行目录下的 `data` 文件夹。
3. `Directory.CreateDirectory(dataDirectory)` 确保这个文件夹存在。

注意：

> 这里创建的是 `data` 文件夹，不是 `spiritdesk.db` 数据库文件。

---

## 3. 第三步：确定数据库文件路径

代码：

```csharp
var databasePath = Path.Combine(dataDirectory, "spiritdesk.db");
```

含义：

```text
dataDirectory + spiritdesk.db = 数据库文件完整路径
```

例如可能是：

```text
D:\Users\zyx\Desktop\DesktopCompanion\src\SpiritDesk.Web\bin\Debug\net9.0\data\spiritdesk.db
```

或者 Shell 启动时对应桌面程序输出目录下的：

```text
...\SpiritDesk.Shell\bin\Debug\net9.0-windows\data\spiritdesk.db
```

具体位置取决于程序从哪里运行，以及 `SpiritDesk:DataDirectory` 是否被配置覆盖。

---

## 4. 第四步：启动前检查已有数据库

代码：

```csharp
DatabaseBootstrapper.EnsureCompatibleDatabase(databasePath);
```

这个方法在：

```text
src/SpiritDesk.Web/Helpers/DatabaseBootstrapper.cs
```

它的作用不是创建新数据库，而是处理“已经存在的旧数据库”。

逻辑是：

| 情况 | 处理 |
|------|------|
| `spiritdesk.db` 不存在 | 直接返回，等 EF Core 后面创建 |
| `spiritdesk.db` 存在且结构兼容 | 继续使用 |
| `spiritdesk.db` 存在但缺少必要列 | 备份旧库，然后删除原库，让 EF Core 后面重建 |
| `WebAccounts` 表不存在 | 用 `CREATE TABLE IF NOT EXISTS` 补建 |

答辩可以说：

> `DatabaseBootstrapper` 是启动前的兼容性检查，真正新建数据库主要靠 EF Core 的 `EnsureCreatedAsync()`。

---

## 5. 第五步：注册 EF Core 和 SQLite

代码：

```csharp
var connectionString = builder.Configuration.GetConnectionString("SpiritDesk");
builder.Services.AddDbContext<SpiritDeskDbContext>(optionsBuilder =>
    optionsBuilder.UseSqlite(string.IsNullOrWhiteSpace(connectionString)
        ? $"Data Source={databasePath}"
        : connectionString));
```

这段非常关键。

它告诉 ASP.NET Core：

1. 项目里要使用 `SpiritDeskDbContext`。
2. `SpiritDeskDbContext` 使用 SQLite。
3. 如果配置里没有写连接串，就连接默认的 `data/spiritdesk.db`。
4. 以后 Service 或 PageModel 需要 DbContext 时，由依赖注入自动提供。

注意：

> 这一步只是“注册规则”，还没有真正创建数据库文件。

---

## 6. 第六步：页面或 API 调用业务服务

数据库真正创建，通常发生在用户访问页面或 Shell 调 API 时。

例如：

```text
浏览器打开首页
  ↓
Index.cshtml.cs
  ↓
SpiritDeskService.BuildViewModelAsync()
  ↓
EnsureInitializedAsync()
```

或者桌面浮球调用：

```text
Shell 浮球
  ↓
GET /api/companion/current
  ↓
SpiritDeskService.EnsureInitializedAsync()
```

也就是说：

> Web 宿主启动时会准备好数据库配置，真正建库通常在业务服务第一次初始化时触发。

---

## 7. 第七步：EnsureCreatedAsync 真正建库建表

核心代码在：

```text
src/SpiritDesk.Web/Services/SpiritDeskService.cs
```

方法：

```csharp
public async Task EnsureInitializedAsync()
{
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
}
```

最关键的是第一行：

```csharp
await dbContext.Database.EnsureCreatedAsync();
```

它的作用：

- 如果数据库不存在，就创建数据库文件。
- 如果表不存在，就根据 EF Core 模型创建表。
- 如果数据库已经存在，一般不会重复创建。

EF Core 创建表时参考两类信息：

| 来源 | 作用 |
|------|------|
| `Core/Entities/*.cs` | 决定有哪些实体类、属性、类型 |
| `SpiritDeskDbContext.OnModelCreating` | 决定主键、索引、唯一约束、值转换、种子数据 |

---

## 8. 第八步：OnModelCreating 决定表结构细节

数据库表不是凭空生成的，EF Core 会读取：

```text
src/SpiritDesk.Web/Data/SpiritDeskDbContext.cs
```

例如：

```csharp
public DbSet<TaskItem> Tasks => Set<TaskItem>();
```

表示会有任务表。

再例如：

```csharp
modelBuilder.Entity<WebAccount>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.HasIndex(x => x.Username).IsUnique();
});
```

表示：

- `WebAccounts` 表使用 `Id` 做主键
- `Username` 有唯一索引

所以：

> `EnsureCreatedAsync()` 是触发建库建表的动作，`SpiritDeskDbContext` 是告诉 EF Core 应该建成什么样。

---

## 9. 第九步：写入五精灵种子数据

`SpiritDeskDbContext.cs` 里有：

```csharp
modelBuilder.Entity<SpiritDefinition>().HasData(...)
```

`HasData` 表示种子数据。

第一次创建数据库时，会写入五精灵：

- 卷卷晴
- 嘻嘻滴
- 贴贴朵
- 慢慢壤
- 新新星

另外，`SpiritDeskService.EnsureSpiritDefinitionsAsync()` 还会再次同步精灵定义：

```csharp
var existing = await dbContext.Spirits.FirstOrDefaultAsync(x => x.Id == definition.Id);
if (existing is null)
    dbContext.Spirits.Add(definition);
else
    existing.Name = definition.Name;
```

这样即使数据库已经存在，代码里的精灵文案更新后，也能同步到数据库。

---

## 10. 第十步：补充业务初始数据

建表后，Service 还会保证当前用户有档案：

```csharp
var profile = await EnsureCurrentProfileAsync();
```

如果当前账号还没有 `UserProfile`，就创建一个。

之后，如果该用户没有任务和聊天记录，会插入演示数据：

```csharp
dbContext.Tasks.AddRange(BuildDemoTasks(profile.Id));
dbContext.ChatMessages.AddRange(BuildDemoChatMessages(profile.Id, profile.CurrentSpiritId));
await dbContext.SaveChangesAsync();
```

所以数据库首次创建后，不只是空表，通常还会有：

- 五精灵配置
- 当前用户档案
- 几条演示任务
- 几条演示聊天

---

## 11. 已有数据库为什么还要补列？

`EnsureCreatedAsync()` 有一个特点：

> 它适合从零创建数据库，但不会像正式 Migration 那样自动升级已有表结构。

所以本项目又写了几个补列方法：

```csharp
await EnsureUserProfileSchemaAsync();
await EnsureTaskSchemaAsync();
await EnsureChatMessageSchemaAsync();
await EnsureDailyActionLogSchemaAsync();
```

这些方法会：

- 用 `PRAGMA table_info(...)` 查看表里有哪些列
- 如果缺列，就执行 `ALTER TABLE ... ADD COLUMN`
- 如果缺索引，就执行 `CREATE INDEX IF NOT EXISTS`

例如：

```csharp
await AddColumnIfMissingAsync(connection, columns, "Tasks", "UserProfileId", "INTEGER NULL");
```

含义：

> 如果 `Tasks` 表缺少 `UserProfileId` 列，就补上这列。

这属于课程项目里的轻量兼容方案。

---

## 12. 最终创建流程图

```text
运行项目
  ↓
SpiritDeskWebHost.Build()
  ↓
读取配置 SpiritDesk:DataDirectory
  ↓
没有配置则使用 AppContext.BaseDirectory/data
  ↓
Directory.CreateDirectory(dataDirectory)
  ↓
databasePath = dataDirectory/spiritdesk.db
  ↓
DatabaseBootstrapper 检查已有库
  ↓
AddDbContext + UseSqlite 注册 EF Core
  ↓
用户访问页面或 Shell 调 API
  ↓
SpiritDeskService.EnsureInitializedAsync()
  ↓
dbContext.Database.EnsureCreatedAsync()
  ↓
根据 Entity + DbContext 建 SQLite 表
  ↓
HasData 写入五精灵
  ↓
Service 补列、同步精灵、创建用户档案和演示数据
  ↓
SaveChangesAsync 写入数据库
```

---

## 13. 老师可能怎么问

**问：数据库文件是谁创建的？**  
> 是 EF Core 创建的。业务初始化时调用 `dbContext.Database.EnsureCreatedAsync()`，如果 `spiritdesk.db` 不存在，EF Core 会根据实体类和 DbContext 配置创建 SQLite 数据库和表。

**问：`SpiritDeskDbContext.cs` 会直接创建数据库吗？**  
> 它本身只是描述模型和映射规则，真正触发创建的是 `EnsureCreatedAsync()`。

**问：`SpiritDeskWebHost.cs` 做了什么？**  
> 它确定 `data/spiritdesk.db` 路径，并通过 `AddDbContext` 和 `UseSqlite` 注册 EF Core 数据库连接。

**问：`DatabaseBootstrapper` 是不是建库的？**  
> 不是主要建库逻辑。它负责检查已有 SQLite 文件是否兼容，必要时备份旧库，让 EF Core 后续重建。

**问：初始五精灵数据从哪来？**  
> 一部分来自 `SpiritDeskDbContext.OnModelCreating` 里的 `HasData`，另一部分由 `SpiritDeskService.EnsureSpiritDefinitionsAsync()` 同步维护。

上一篇：[01-数据库基础知识.md](./01-数据库基础知识.md)  
下一篇：[03-SpiritDeskDbContext逐段拆解.md](./03-SpiritDeskDbContext逐段拆解.md)
