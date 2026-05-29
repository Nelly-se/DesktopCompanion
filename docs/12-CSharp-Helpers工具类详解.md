# C# Helpers 工具类详解

本文专门说明 `src/SpiritDesk.Web/Helpers/` 文件夹里的代码。这个文件夹放的是 Web 项目运行时会用到的“辅助工具类”：它们不是页面，也不是业务服务，而是给启动流程、配置读取、时间文案等场景提供小而独立的公共功能。

## 1. Helpers 文件夹是干什么的

`Helpers` 可以理解为“工具箱”。项目中有些逻辑不适合放在页面里，也不适合放进 `SpiritDeskService` 这种业务服务里，例如：

- 启动时读取 `.env` 配置；
- 根据当前时间生成一句问候语；
- 启动前检查本地 SQLite 数据库结构是否兼容。

这些逻辑都有一个共同点：它们功能明确、复用性较强、依赖较少，所以单独放进 `Helpers` 文件夹，让主流程调用起来更清楚。

## 2. 文件总览

| 文件 | 类名 | 主要作用 | 被谁调用 |
|------|------|----------|----------|
| `DotEnvLoader.cs` | `DotEnvLoader` | 查找并加载 `.env` 文件，把里面的配置写入环境变量 | `SpiritDeskWebHost.Build` |
| `DateTimeHelper.cs` | `DateTimeHelper` | 根据小时返回“早上好 / 下午好 / 晚上好”等问候语 | 页面或视图模型需要时间文案时使用 |
| `DatabaseBootstrapper.cs` | `DatabaseBootstrapper` | 启动时检查 SQLite 表结构，不兼容则备份旧库并重建 | `SpiritDeskWebHost.Build` |

## 3. `DotEnvLoader.cs`：加载本地环境变量

### 它解决什么问题

项目里调用大模型时需要 API Key，例如 `ARK_API_KEY` 这一类敏感配置。API Key 不应该直接写死在代码里，也不应该提交到 Git 仓库。因此本地开发时可以把它放进 `.env` 文件。

`DotEnvLoader` 的作用就是：程序启动时自动找到 `.env` 文件，把里面的 `KEY=VALUE` 配置读出来，写入当前进程的环境变量。

### 什么时候执行

在 `SpiritDeskWebHost.Build` 开头执行：

```csharp
var resolvedContentRoot = contentRoot ?? Directory.GetCurrentDirectory();
DotEnvLoader.LoadNearest(resolvedContentRoot);
```

这意味着 Web 项目刚开始组装宿主时，就先尝试加载 `.env`。后面配置服务、大模型服务、认证服务时，就能读取这些环境变量。

### 核心流程

1. `LoadNearest(startDirectory)` 从启动目录开始找 `.env`。
2. 如果当前目录没有，就向父目录继续找。
3. 找到第一个 `.env` 后调用 `LoadFile(path)`。
4. `LoadFile` 逐行读取文件。
5. 空行和 `#` 开头的注释行会跳过。
6. 支持普通写法 `KEY=VALUE`。
7. 也支持 `export KEY=VALUE` 写法。
8. 如果系统环境变量里已经有同名 key，就不覆盖。
9. 如果值被单引号或双引号包住，会去掉首尾引号。

### 为什么不覆盖已有环境变量

部署到服务器或容器时，环境变量通常由平台注入。例如云服务器上可能已经设置了正式的 API Key。此时本地 `.env` 不应该覆盖线上配置，所以代码里有这段保护：

```csharp
if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
{
    continue;
}
```

答辩时可以这样讲：

> `DotEnvLoader` 是本地开发配置加载器。它从当前目录往上找 `.env`，把 API Key 这类敏感配置放进环境变量，避免写死在代码里，同时不会覆盖服务器已经提供的正式环境变量。

## 4. `DateTimeHelper.cs`：根据时间生成问候语

### 它解决什么问题

项目的页面或精灵文案里可能需要根据当前时间显示不同问候，例如“早上好”“下午好”“晚上好”。如果每个页面都自己写一遍判断逻辑，代码会重复，也不方便统一修改。

`DateTimeHelper` 把这个判断集中到一个方法里：

```csharp
DateTimeHelper.GetGreetingByHour(DateTime.Now)
```

### 规则说明

| 时间范围 | 返回文案 |
|----------|----------|
| 0 点到 5 点 | `夜深了` |
| 6 点到 11 点 | `早上好` |
| 12 点到 17 点 | `下午好` |
| 18 点到 23 点 | `晚上好` |

### 代码特点

这个类是一个纯工具类：

- 不访问数据库；
- 不读取配置；
- 不修改任何全局状态；
- 只根据传入的 `DateTime` 返回字符串。

因此它非常容易理解，也方便测试。给它一个固定时间，就能得到固定结果。

答辩时可以这样讲：

> `DateTimeHelper` 负责把时间转换成页面上的中文问候语。它没有数据库依赖，也没有副作用，是一个很简单的静态工具类。

## 5. `DatabaseBootstrapper.cs`：启动前检查 SQLite 数据库

### 它解决什么问题

项目使用 SQLite 作为本地数据库，数据库文件一般是 `spiritdesk.db`。开发过程中实体类可能变化，例如增加字段、增加账号表。如果本地旧数据库结构和当前代码不一致，程序启动后就可能报错。

`DatabaseBootstrapper` 的作用是：启动时先检查旧数据库是否兼容当前代码。如果不兼容，就把旧数据库备份起来，让后面的 EF Core 初始化流程创建新数据库。

### 它和 EF Migration 的区别

正式项目里通常会使用 EF Core Migration 管理数据库结构变化。这个项目里 `DatabaseBootstrapper` 更像一个课程项目的轻量兜底方案：

- 它不生成 migration 文件；
- 它不做复杂的数据升级；
- 它只判断核心表是否缺少必要列；
- 如果不兼容，就备份旧库并重建。

所以它适合本地演示和课程开发，能减少“旧数据库导致启动失败”的问题。

### 核心数据结构

文件里最重要的数据结构是 `RequiredSchema`：

```csharp
private static readonly IReadOnlyDictionary<string, string[]> RequiredSchema =
    new Dictionary<string, string[]>
    {
        ["UserProfiles"] = [ "Id", "Nickname", "CurrentSpiritId", ... ],
        ["Spirits"] = [ "Id", "Name", "Mbti", ... ],
        ["Tasks"] = [ "Id", "Title", "Description", ... ],
        ["ChatMessages"] = [ "Id", "Sender", "Content", ... ],
        ["DailyActionLogs"] = [ "Id", "ActionDate", "ActionType", "Count" ]
    };
```

它的意思是：

- key 是表名；
- value 是这张表必须存在的字段名；
- 启动检查时会逐表验证。

### 启动检查流程

1. `SpiritDeskWebHost.Build` 计算数据库路径 `data/spiritdesk.db`。
2. 调用 `DatabaseBootstrapper.EnsureCompatibleDatabase(databasePath)`。
3. 如果数据库文件不存在，直接返回，交给 EF Core 后续创建。
4. 如果数据库存在，用 `SqliteConnection` 打开。
5. 遍历 `RequiredSchema` 里的每张表。
6. 对每张表执行 `PRAGMA table_info("表名")`。
7. 把 SQLite 返回的列名放入 `HashSet<string>`。
8. 判断必需列是否全部存在。
9. 如果缺表或缺字段，调用 `ResetDatabase`。
10. 如果核心表都兼容，再调用 `EnsureWebAccountsTable` 补建登录账号表。

### 为什么用 `HashSet`

读取表结构后，代码会把列名放到：

```csharp
var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
```

原因是后面要多次判断“某个字段是否存在”。`HashSet.Contains` 查询速度快，而且这里设置了 `StringComparer.OrdinalIgnoreCase`，字段名大小写不同也能兼容。

### `EnsureWebAccountsTable` 做什么

`WebAccounts` 是登录注册功能需要的账号表。对于已经存在的旧数据库，EF Core 的 `EnsureCreated` 不一定会自动给旧库补新表，所以这里手写了：

```sql
CREATE TABLE IF NOT EXISTS "WebAccounts" (...);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_WebAccounts_Username" ON "WebAccounts" ("Username");
```

`IF NOT EXISTS` 表示表已经存在时不会重复创建，因此这个方法可以安全地多次执行。

### `ResetDatabase` 做什么

如果发现数据库结构不兼容，`ResetDatabase` 会：

1. 计算备份文件名，例如 `spiritdesk.backup-20260522-084800.db`。
2. 先尝试删除 SQLite 的 `-shm` 和 `-wal` 辅助文件。
3. 把旧的 `spiritdesk.db` 移动成备份文件。
4. 如果移动失败，再尝试删除旧数据库。
5. 在控制台输出重置信息。

这样做的好处是：尽量保留旧数据备份，同时保证程序下次启动时可以创建干净的新数据库。

答辩时可以这样讲：

> `DatabaseBootstrapper` 是数据库启动兼容检查器。它会在 Web 宿主启动时检查 SQLite 旧库的核心表结构。如果旧库缺表或缺字段，就先备份旧库，再让 EF Core 重新创建数据库，避免本地演示时因为旧数据库结构不一致导致程序启动失败。

## 6. 三个 Helper 在启动流程中的位置

整体顺序可以这样理解：

```text
程序启动
  ↓
SpiritDeskWebHost.Build
  ↓
DotEnvLoader.LoadNearest
  读取 .env，准备 API Key 等配置
  ↓
创建 data 目录和 spiritdesk.db 路径
  ↓
DatabaseBootstrapper.EnsureCompatibleDatabase
  检查旧 SQLite 数据库是否兼容
  ↓
注册 EF Core、业务服务、Razor Pages、认证等
  ↓
页面或服务需要时间文案时调用 DateTimeHelper
```

## 7. 老师提问怎么答

### Q1：为什么要有 Helpers 文件夹？

可以答：

> Helpers 放的是通用辅助逻辑，不直接代表某个页面或某个业务功能。比如读取 `.env`、生成问候语、检查数据库结构，这些逻辑独立性比较强，单独放在 Helpers 里能让主流程更清晰。

### Q2：`DotEnvLoader` 为什么不直接写在 `Program.cs` 里？

可以答：

> 读取 `.env` 是一个独立的配置加载动作。单独封装成 `DotEnvLoader` 后，`Program.cs` 或 `SpiritDeskWebHost` 只需要调用一行，启动流程更干净，也方便以后复用或修改 `.env` 解析规则。

### Q3：`DatabaseBootstrapper` 是不是数据库迁移？

可以答：

> 不是严格意义上的 EF Migration。它是启动前的兼容性检查工具，主要用于课程项目和本地演示。当旧 SQLite 文件结构不匹配时，它会备份旧库并让程序重建数据库。正式生产项目通常会使用 EF Core Migration 做更细的数据迁移。

### Q4：`DateTimeHelper` 为什么做成静态类？

可以答：

> 因为它没有状态，也不依赖数据库或配置，只根据传入时间返回一句文案。做成静态工具类使用简单，不需要依赖注入。

## 8. 一句话总结

`Helpers` 文件夹负责放置 Web 项目的辅助工具代码：`DotEnvLoader` 管配置加载，`DateTimeHelper` 管时间问候语，`DatabaseBootstrapper` 管本地 SQLite 启动兼容检查。它们让主启动流程和业务服务保持简洁，把边缘但必要的功能单独封装起来。
