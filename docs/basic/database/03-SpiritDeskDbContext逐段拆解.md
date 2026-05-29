# 03 - SpiritDeskDbContext.cs 逐段拆解

> 本篇专门拆解这个文件：  
> `src/SpiritDesk.Web/Data/SpiritDeskDbContext.cs`

它不是直接“手写建表 SQL”的地方，而是 **告诉 EF Core：项目有哪些表、表怎么约束、初始数据是什么**。

---

## 0. 先看它在数据库创建中的位置

```text
SpiritDeskWebHost.cs
  负责确定数据库路径 + 注册 UseSqlite
        ↓
SpiritDeskService.EnsureInitializedAsync()
  调用 EnsureCreatedAsync 触发建库建表
        ↓
SpiritDeskDbContext.cs
  提供建表依据：DbSet + OnModelCreating + HasData
        ↓
SQLite 文件 spiritdesk.db
```

一句话：

> `SpiritDeskDbContext.cs` 是数据库模型说明书，EF Core 根据它生成 SQLite 表结构。

---

## 1. 文件头注释

```csharp
// SpiritDeskDbContext.cs — EF Core DbContext（对象 ↔ SQLite 表映射中心）
```

这句话说明它的定位：

- EF Core 的 `DbContext`
- 负责 C# 对象和 SQLite 表之间的映射
- 是本项目数据库访问的核心文件

文件头还提到三点：

| 注释内容 | 含义 |
|----------|------|
| `DbSet<T>` | 每张表在 C# 代码中的入口 |
| `ModelBuilder` | 配置主键、索引、种子数据、类型转换 |
| `AddDbContext` 注册为 Scoped | 每个 HTTP 请求一般使用一个上下文 |

---

## 2. using 引用

```csharp
using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Constants;
using SpiritDesk.Core.Entities;
```

逐个解释：

| using | 作用 |
|-------|------|
| `Microsoft.EntityFrameworkCore` | 使用 EF Core 的 `DbContext`、`DbSet`、`ModelBuilder` |
| `SpiritDesk.Core.Constants` | 使用 `SpiritIds` 常量，保证五精灵 Id 统一 |
| `SpiritDesk.Core.Entities` | 使用 `UserProfile`、`TaskItem`、`ChatMessage` 等实体类 |

这里说明了一个结构设计：

> 实体类放在 `SpiritDesk.Core`，数据库映射放在 `SpiritDesk.Web`。

---

## 3. namespace 命名空间

```csharp
namespace SpiritDesk.Web.Data;
```

含义：

- 这个类属于 Web 项目的 Data 层
- 其他文件引用它时使用 `SpiritDesk.Web.Data`
- 从目录结构上看，它就是 Web 项目的数据访问入口

---

## 4. 类声明

```csharp
public class SpiritDeskDbContext(DbContextOptions<SpiritDeskDbContext> options) : DbContext(options)
```

这一行信息很多，拆开看：

| 片段 | 含义 |
|------|------|
| `public class SpiritDeskDbContext` | 定义项目自己的数据库上下文 |
| `DbContextOptions<SpiritDeskDbContext> options` | 接收 EF Core 配置，比如 SQLite 连接串 |
| `: DbContext(options)` | 继承 EF Core 的 `DbContext`，并把配置传给父类 |

这是 C# 12 的主构造函数写法。旧写法大概等价于：

```csharp
public class SpiritDeskDbContext : DbContext
{
    public SpiritDeskDbContext(DbContextOptions<SpiritDeskDbContext> options)
        : base(options)
    {
    }
}
```

答辩可以说：

> `SpiritDeskDbContext` 继承 EF Core 的 `DbContext`，构造时接收数据库连接配置，这些配置由 `AddDbContext` 注入。

---

## 5. UserProfiles 表

```csharp
public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
```

含义：

| 代码 | 数据库理解 |
|------|------------|
| `UserProfile` | 用户档案实体类 |
| `UserProfiles` | 数据库里的用户档案表 |
| `dbContext.UserProfiles` | 查询 / 新增 / 修改用户档案的入口 |

保存内容包括：

- 昵称
- 当前精灵
- 心情值
- 亲密度
- 等级
- 金币
- 当前登录账号绑定名

---

## 6. WebAccounts 表

```csharp
public DbSet<WebAccount> WebAccounts => Set<WebAccount>();
```

含义：

| 代码 | 数据库理解 |
|------|------------|
| `WebAccount` | 登录账号实体类 |
| `WebAccounts` | 登录账号表 |

保存内容包括：

- 用户名
- 密码哈希
- 创建时间

注意：

> 这里保存的是密码哈希，不是明文密码。

---

## 7. Spirits 表

```csharp
public DbSet<SpiritDefinition> Spirits => Set<SpiritDefinition>();
```

含义：

- `SpiritDefinition` 是五精灵配置实体
- `Spirits` 是数据库中的精灵表
- 五精灵的名称、MBTI、立绘路径、颜色、机制加成都存在这里

例如：

- 卷卷晴
- 嘻嘻滴
- 贴贴朵
- 慢慢壤
- 新新星

---

## 8. Tasks 表

```csharp
public DbSet<TaskItem> Tasks => Set<TaskItem>();
```

含义：

- `TaskItem` 是待办任务实体
- `Tasks` 是任务表
- 每一行是一条待办

常见字段：

- 标题
- 描述
- 截止时间
- 是否完成
- 所属用户档案 `UserProfileId`

---

## 9. ChatMessages 表

```csharp
public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
```

含义：

- `ChatMessage` 是聊天消息实体
- `ChatMessages` 是聊天记录表
- 每一行是一条用户或精灵发出的消息

常见字段：

- 发送者
- 内容
- 所属精灵
- 所属用户档案
- 创建时间

---

## 10. DailyActionLogs 表

```csharp
public DbSet<DailyActionLog> DailyActionLogs => Set<DailyActionLog>();
```

含义：

- `DailyActionLog` 是每日动作记录实体
- `DailyActionLogs` 是每日互动表
- 用来记录某个用户某天做了几次签到、投喂、小游戏等操作

---

## 11. DbSet 总结

这一组代码：

```csharp
public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
public DbSet<WebAccount> WebAccounts => Set<WebAccount>();
public DbSet<SpiritDefinition> Spirits => Set<SpiritDefinition>();
public DbSet<TaskItem> Tasks => Set<TaskItem>();
public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
public DbSet<DailyActionLog> DailyActionLogs => Set<DailyActionLog>();
```

可以整体理解为：

> 告诉 EF Core：这个数据库里有六类核心数据，每类数据都可以通过一个 `DbSet` 操作。

---

## 12. OnModelCreating 是什么？

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
```

`OnModelCreating` 是 EF Core 提供的模型配置方法。

它会在 EF Core 构建数据库模型时执行，用来补充说明：

- 哪个字段是主键
- 哪些字段要建索引
- 哪些字段必须唯一
- 字段最大长度是多少
- 某些 C# 类型如何存进数据库
- 初始种子数据有哪些

简单说：

> `DbSet` 告诉 EF Core 有哪些表，`OnModelCreating` 告诉 EF Core 这些表有哪些规则。

---

## 13. WebAccount 配置

```csharp
modelBuilder.Entity<WebAccount>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.HasIndex(x => x.Username).IsUnique();
    entity.Property(x => x.Username).HasMaxLength(64).IsRequired();
    entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
});
```

逐句解释：

| 代码 | 含义 |
|------|------|
| `Entity<WebAccount>` | 配置 `WebAccount` 这张表 |
| `HasKey(x => x.Id)` | `Id` 是主键 |
| `HasIndex(x => x.Username)` | 给用户名建索引 |
| `IsUnique()` | 用户名不能重复 |
| `HasMaxLength(64)` | 用户名最长 64 |
| `IsRequired()` | 字段不能为空 |
| `PasswordHash` 最长 500 | 给密码哈希预留长度 |

这段主要服务登录注册功能。

---

## 14. UserProfile 配置

```csharp
modelBuilder.Entity<UserProfile>(entity =>
{
    entity.HasIndex(x => x.AccountUsername).IsUnique();
    entity.Property(x => x.AccountUsername).HasMaxLength(64);
});
```

含义：

- `AccountUsername` 用来把用户档案和登录账号绑定
- 给 `AccountUsername` 建唯一索引
- 一个登录账号只对应一个用户档案
- 用户名最长 64

注意：

> `WebAccounts` 管登录，`UserProfiles` 管养成进度，它们通过规范化用户名关联。

---

## 15. UserProfileId 索引

```csharp
modelBuilder.Entity<TaskItem>().HasIndex(x => x.UserProfileId);
modelBuilder.Entity<ChatMessage>().HasIndex(x => x.UserProfileId);
```

含义：

- 任务按 `UserProfileId` 归属到某个用户
- 聊天记录也按 `UserProfileId` 归属到某个用户
- 建索引后，查询“当前用户的任务 / 聊天”更快

例如业务层会查询：

```csharp
dbContext.Tasks.Where(x => x.UserProfileId == profile.Id)
```

---

## 16. DailyActionLog 唯一索引

```csharp
modelBuilder.Entity<DailyActionLog>()
    .HasIndex(x => new { x.UserProfileId, x.ActionDate, x.ActionType })
    .IsUnique();
```

含义：

同一个用户、同一天、同一种动作，只允许有一行记录。

例如：

| UserProfileId | ActionDate | ActionType | Count |
|---------------|------------|------------|-------|
| 1 | 2026-05-22 | feed | 3 |

如果用户今天投喂三次，不是插入三行，而是同一行的 `Count` 增加。

---

## 17. SpiritDefinition 主键

```csharp
modelBuilder.Entity<SpiritDefinition>().HasKey(x => x.Id);
```

含义：

- `SpiritDefinition.Id` 是主键
- 精灵 Id 不是自增数字，而是稳定字符串常量

例如：

```text
light
water
air
soil
nutrition
```

这样代码里可以稳定引用某个精灵。

---

## 18. DateOnly 值转换

```csharp
modelBuilder.Entity<DailyActionLog>().Property(x => x.ActionDate).HasConversion(
    value => value.ToDateTime(TimeOnly.MinValue),
    value => DateOnly.FromDateTime(value));
```

`DailyActionLog.ActionDate` 在 C# 里是 `DateOnly`，表示只有日期，没有具体时分秒。

但 SQLite 原生类型比较简单，所以这里告诉 EF Core：

| 方向 | 转换 |
|------|------|
| C# 写入 SQLite | `DateOnly` 转成当天 00:00 的 `DateTime` |
| SQLite 读回 C# | `DateTime` 再转回 `DateOnly` |

这就是“值转换”。

---

## 19. 五精灵 HasData 种子数据

```csharp
modelBuilder.Entity<SpiritDefinition>().HasData(
    new SpiritDefinition
    {
        Id = SpiritIds.Light,
        Name = "卷卷晴",
        Mbti = "ENTJ",
        Title = "工作学习发动机",
        ...
    },
    ...
);
```

`HasData` 的作用：

> 第一次创建数据库时，EF Core 自动插入这些基础数据。

本项目写入五条 `SpiritDefinition`：

| Id | 名称 | 定位 |
|----|------|------|
| `SpiritIds.Light` | 卷卷晴 | 工作学习发动机 |
| `SpiritIds.Water` | 嘻嘻滴 | 快乐补给站 |
| `SpiritIds.Air` | 贴贴朵 | 人际关系维护师 |
| `SpiritIds.Soil` | 慢慢壤 | 身体与情绪的养护师 |
| `SpiritIds.Nutrition` | 新新星 | 兴趣发展试验家 |

这说明精灵不是写死在页面里的，而是作为数据库中的基础配置存在。

---

## 20. 这个文件不会做什么？

`SpiritDeskDbContext.cs` 很重要，但要注意它不负责所有事。

| 它不负责 | 谁负责 |
|----------|--------|
| 决定 `spiritdesk.db` 放在哪 | `SpiritDeskWebHost.cs` |
| 注册 SQLite 连接串 | `SpiritDeskWebHost.cs` |
| 真正触发创建数据库 | `SpiritDeskService.EnsureInitializedAsync()` 里的 `EnsureCreatedAsync()` |
| 检查旧数据库兼容性 | `DatabaseBootstrapper.cs` |
| 给当前用户创建档案和演示任务 | `SpiritDeskService.cs` |
| 页面展示数据 | Razor Pages 和 ViewModel |

所以：

> `DbContext` 是数据库模型中心，不是整个数据库生命周期的全部。

---

## 21. 从这个文件推导出的表结构

根据 `DbSet` 和实体类，EF Core 会创建这些表：

| DbSet | 表名 | 用途 |
|-------|------|------|
| `UserProfiles` | `UserProfiles` | 用户档案 |
| `WebAccounts` | `WebAccounts` | 登录账号 |
| `Spirits` | `Spirits` | 精灵配置 |
| `Tasks` | `Tasks` | 待办任务 |
| `ChatMessages` | `ChatMessages` | 聊天记录 |
| `DailyActionLogs` | `DailyActionLogs` | 每日动作次数 |

表的具体列来自对应实体类的属性，再加上 `OnModelCreating` 里的约束配置。

---

## 22. 老师可能怎么问

**问：`SpiritDeskDbContext.cs` 是干什么的？**  
> 它是 EF Core 的数据库上下文，负责声明项目有哪些表，并配置主键、索引、字段规则、类型转换和五精灵种子数据。

**问：`DbSet` 是什么？**  
> `DbSet` 是某张表在 C# 代码里的操作入口，比如 `dbContext.Tasks` 对应任务表。

**问：`OnModelCreating` 是什么？**  
> 它是 EF Core 配置数据库模型的方法，用来配置主键、唯一索引、字段长度、值转换和种子数据。

**问：五精灵数据在哪里初始化？**  
> 在 `SpiritDeskDbContext.OnModelCreating` 里的 `HasData` 中配置，创建数据库时 EF Core 会写入。

**问：这个文件会直接创建 `spiritdesk.db` 吗？**  
> 不会。它提供模型配置，真正创建数据库由 `EnsureCreatedAsync()` 触发。

上一篇：[02-spiritdesk-db是怎么创建出来的.md](./02-spiritdesk-db是怎么创建出来的.md)
