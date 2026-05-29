# 17 - DbContext 是什么？（EF Core 数据库上下文专讲）

> 前面 [13-EF是什么详解.md](./13-EF是什么详解.md) 讲了 EF Core 是什么，  
> [16-数据访问层与对象关系映射详解.md](./16-数据访问层与对象关系映射详解.md) 讲了数据访问层和 ORM。  
> 这一篇只讲一个关键词：**DbContext**。

---

## 0. 一句话（先背）

**DbContext 是 EF Core 访问数据库的入口和工作区。**  
它知道项目里有哪些表、这些表对应哪些 C# 实体类、连接哪个数据库，并负责跟踪对象变化，最后通过 `SaveChangesAsync()` 把变化保存到数据库。

答辩可以这样说：

> `DbContext` 是 EF Core 的数据库上下文。本项目的 `SpiritDeskDbContext` 继承自 EF Core 的 `DbContext`，里面通过 `DbSet` 声明用户、任务、聊天、精灵等表，并在 `OnModelCreating` 中配置主键、索引、字段规则和五精灵种子数据。

---

## 1. 为什么叫“上下文”？

`Context` 中文常翻译成“上下文”，听起来抽象，其实可以理解成：

> 一次数据库操作过程中的“工作环境”。

它包含这些信息：

- 当前连接的是哪个数据库
- 数据库里有哪些表
- 表和 C# 类怎么对应
- 哪些对象被查询出来了
- 哪些对象被新增、修改、删除了
- 什么时候把变化提交到数据库

所以 `DbContext` 不只是“连接数据库”，它还负责管理整个 EF Core 操作数据库的过程。

---

## 2. 用生活例子理解 DbContext

可以把数据库想象成一个仓库：

| 概念 | 类比 |
|------|------|
| SQLite 数据库 | 仓库本身 |
| 表 | 仓库里的不同货架 |
| Entity 实体类 | 货物的登记模板 |
| DbSet | 某个货架的操作入口 |
| DbContext | 仓库管理员 + 工作台 |

当代码执行：

```csharp
var tasks = await dbContext.Tasks.ToListAsync();
```

就像对仓库管理员说：

> 去 `Tasks` 这个货架，把所有任务记录拿出来。

当代码执行：

```csharp
dbContext.Tasks.Add(new TaskItem { Title = "完成实验报告" });
await dbContext.SaveChangesAsync();
```

就像：

> 先在工作台登记一条新任务，再确认入库。

---

## 3. 本项目的 DbContext 在哪里？

文件：

```text
src/SpiritDesk.Web/Data/SpiritDeskDbContext.cs
```

核心代码：

```csharp
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
        // 配置主键、索引、字段长度、种子数据等
    }
}
```

这个类的名字是 `SpiritDeskDbContext`，意思就是：

> SpiritDesk 这个项目专用的数据库上下文。

---

## 4. DbContext 主要做哪几件事？

### 4.1 接收数据库连接配置

构造函数这一行：

```csharp
public class SpiritDeskDbContext(DbContextOptions<SpiritDeskDbContext> options) : DbContext(options)
```

意思是：创建 `SpiritDeskDbContext` 时，需要传入 EF Core 的配置选项。

这些配置选项包括：

- 使用哪种数据库：SQLite
- 数据库文件在哪：`data/spiritdesk.db`
- EF Core 如何创建数据库连接

这些选项不是手动 new 出来的，而是在 Web 启动时注册的。

---

### 4.2 声明数据库里有哪些表

`DbContext` 里最常见的就是 `DbSet`：

```csharp
public DbSet<TaskItem> Tasks => Set<TaskItem>();
```

可以读成：

> 数据库里有一张任务表，代码里通过 `dbContext.Tasks` 操作它，每一行数据对应一个 `TaskItem` 对象。

本项目的表大致如下：

| DbSet | 实体类 | 存什么 |
|-------|--------|--------|
| `UserProfiles` | `UserProfile` | 用户档案、昵称、当前精灵、心情、金币 |
| `WebAccounts` | `WebAccount` | 登录账号、密码哈希 |
| `Spirits` | `SpiritDefinition` | 五精灵配置 |
| `Tasks` | `TaskItem` | 待办任务 |
| `ChatMessages` | `ChatMessage` | 聊天记录 |
| `DailyActionLogs` | `DailyActionLog` | 每日签到、投喂、游戏次数 |

---

### 4.3 配置表结构规则

`OnModelCreating` 是 EF Core 留给开发者配置模型的地方。

例如：

```csharp
modelBuilder.Entity<WebAccount>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.HasIndex(x => x.Username).IsUnique();
    entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
    entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
});
```

这段告诉 EF Core：

- `WebAccount.Id` 是主键
- `Username` 必须唯一，不能两个账号同名
- `Username` 最长 64 个字符
- `PasswordHash` 必填，最长 500 个字符

这些规则最后会影响 SQLite 表结构。

---

### 4.4 配置种子数据

本项目五精灵不是用户手动创建的，而是系统自带的基础数据。

在 `OnModelCreating` 里有：

```csharp
modelBuilder.Entity<SpiritDefinition>().HasData(
    new SpiritDefinition
    {
        Id = SpiritIds.Light,
        Name = "卷卷晴",
        Title = "工作学习发动机"
    }
);
```

`HasData` 的意思是：

> 创建数据库时，顺便插入这些初始数据。

本项目用它写入五个精灵：

- 卷卷晴
- 嘻嘻滴
- 贴贴朵
- 慢慢壤
- 新新星

---

### 4.5 查询数据

有了 `DbContext`，业务代码就可以这样查数据：

```csharp
var spirits = await dbContext.Spirits
    .OrderBy(x => x.Id)
    .ToListAsync();
```

代码含义：

> 从 `Spirits` 表查询所有精灵，按 `Id` 排序，转成 C# 列表。

这里写的是 C# LINQ，EF Core 会把它翻译成 SQL 去 SQLite 执行。

---

### 4.6 新增和修改数据

新增任务：

```csharp
dbContext.Tasks.Add(new TaskItem
{
    Title = "完成实验报告",
    IsCompleted = false
});

await dbContext.SaveChangesAsync();
```

修改当前精灵：

```csharp
profile.CurrentSpiritId = spiritId;
await dbContext.SaveChangesAsync();
```

关键点：

> 修改对象本身还不等于写入数据库，必须调用 `SaveChangesAsync()`。

---

## 5. SaveChangesAsync 为什么重要？

EF Core 会跟踪从数据库查出来的对象。

例如：

```csharp
var profile = await dbContext.UserProfiles.FirstAsync();
profile.Mood += 5;
```

此时只是 C# 内存里的对象变了。

执行：

```csharp
await dbContext.SaveChangesAsync();
```

EF Core 才会把变化翻译成类似：

```sql
UPDATE UserProfiles SET Mood = Mood + 5 WHERE Id = ...;
```

所以可以记：

| 操作 | 是否立即写入数据库 |
|------|--------------------|
| 查询对象 | 不是修改 |
| 修改对象属性 | 暂时只在内存中 |
| `Add(...)` | 先登记新增 |
| `Remove(...)` | 先登记删除 |
| `SaveChangesAsync()` | 真正提交到数据库 |

---

## 6. DbContext 是怎么被创建出来的？

项目启动时会在 `SpiritDeskWebHost.cs` 中注册：

```csharp
builder.Services.AddDbContext<SpiritDeskDbContext>(optionsBuilder =>
    optionsBuilder.UseSqlite($"Data Source={databasePath}"));
```

含义：

1. 告诉 ASP.NET Core：项目里要使用 `SpiritDeskDbContext`。
2. 告诉 EF Core：数据库类型是 SQLite。
3. 告诉 SQLite：数据库文件路径是 `data/spiritdesk.db`。
4. 以后 PageModel 或 Service 需要 DbContext 时，由依赖注入系统自动提供。

所以业务类里可以写：

```csharp
public class SpiritDeskService(SpiritDeskDbContext dbContext)
{
    // 这里可以直接使用 dbContext
}
```

这叫 **依赖注入**。

---

## 7. Scoped 生命周期是什么意思？

`AddDbContext` 默认把 `DbContext` 注册成 **Scoped**。

在 Web 项目里可以简单理解为：

> 每一次 HTTP 请求，通常使用一个 DbContext 实例。

例如用户打开首页：

```
请求开始
        ↓
创建一个 SpiritDeskDbContext
        ↓
PageModel / Service 使用它查询数据
        ↓
页面生成完成
        ↓
请求结束，DbContext 被释放
```

这样做的好处：

- 同一次请求里的查询和修改可以由同一个上下文跟踪
- 请求结束后及时释放数据库连接资源
- 不需要开发者手动管理连接打开和关闭

答辩不需要讲太深，记住一句：

> DbContext 生命周期一般按请求创建和释放，不是全局永久对象。

---

## 8. DbContext、DbSet、Entity 再对比一次

| 名词 | 作用 | 本项目例子 |
|------|------|------------|
| Entity | 数据实体类，描述一行数据长什么样 | `TaskItem`、`UserProfile` |
| DbSet | 某一张表的操作入口 | `dbContext.Tasks` |
| DbContext | 管理所有表、连接、映射和保存 | `SpiritDeskDbContext` |

关系图：

```
SpiritDeskDbContext
        ├── UserProfiles  -> UserProfile
        ├── WebAccounts   -> WebAccount
        ├── Spirits       -> SpiritDefinition
        ├── Tasks         -> TaskItem
        ├── ChatMessages  -> ChatMessage
        └── DailyActionLogs -> DailyActionLog
```

最短记法：

> Entity 是数据模型，DbSet 是表入口，DbContext 是数据库总入口。

---

## 9. DbContext 和数据库文件是什么关系？

`DbContext` 本身不是数据库文件。

| 名词 | 是什么 |
|------|--------|
| `SpiritDeskDbContext` | C# 代码里的数据库访问入口 |
| `spiritdesk.db` | SQLite 真正保存数据的文件 |
| `DbSet` | 代码里操作某张表的入口 |
| Entity | 表中一行数据对应的 C# 类型 |

可以这样理解：

```
C# 代码
  dbContext.Tasks
        ↓ EF Core
SQLite 文件
  spiritdesk.db 里的 Tasks 表
```

---

## 10. 常见误区

### 10.1 DbContext 不是数据库本身

数据库本身是 `spiritdesk.db` 文件。  
`DbContext` 是 C# 程序访问这个文件的入口。

### 10.2 DbContext 不是一张表

一张表对应的是一个 `DbSet`，例如 `Tasks`。  
`DbContext` 管理很多个 `DbSet`。

### 10.3 改对象后不会自动永久保存

EF Core 可以跟踪对象变化，但真正写入数据库要靠：

```csharp
await dbContext.SaveChangesAsync();
```

### 10.4 Shell 端不直接使用 DbContext

桌面 Shell 不直接访问 SQLite，也不直接 new `SpiritDeskDbContext`。  
它通过 WebView2 / HTTP API 使用 Web 项目的功能，最终由 Web 项目的 `SpiritDeskDbContext` 访问数据库。

---

## 11. 老师可能怎么问

**问：DbContext 是什么？**  
> DbContext 是 EF Core 的数据库上下文，是访问数据库的入口。它管理数据库连接、实体和表的映射、查询、修改跟踪以及保存。

**问：你们项目里的 DbContext 叫什么？在哪？**  
> 叫 `SpiritDeskDbContext`，在 `src/SpiritDesk.Web/Data/SpiritDeskDbContext.cs`。

**问：DbContext 里为什么有很多 DbSet？**  
> 因为每个 `DbSet` 对应一张表，例如 `Tasks` 对应任务表，`ChatMessages` 对应聊天记录表。

**问：OnModelCreating 是干什么的？**  
> 它用来配置数据库模型，比如主键、索引、字段长度、唯一约束、值转换和种子数据。

**问：为什么要调用 SaveChangesAsync？**  
> 因为 EF Core 先在内存中跟踪对象变化，调用 `SaveChangesAsync()` 后才会真正把新增、修改、删除提交到数据库。

**问：DbContext 是谁创建的？**  
> 项目启动时通过 `AddDbContext<SpiritDeskDbContext>()` 注册，运行时由 ASP.NET Core 的依赖注入系统自动创建并传给 PageModel 或 Service。

---

## 12. 相关文档

| 文档 | 内容 |
|------|------|
| [13-EF是什么详解.md](./13-EF是什么详解.md) | EF Core、ORM、SQLite 基础 |
| [16-数据访问层与对象关系映射详解.md](./16-数据访问层与对象关系映射详解.md) | 数据访问层和对象关系映射 |
| [14-数据库文件与迁移详解.md](./14-数据库文件与迁移详解.md) | `spiritdesk.db`、迁移、运行产物 |
| [../03-CSharp-数据层与EF-Core.md](../03-CSharp-数据层与EF-Core.md) | 更偏代码实现的 DbContext 配置 |

上一篇：[16-数据访问层与对象关系映射详解.md](./16-数据访问层与对象关系映射详解.md)
