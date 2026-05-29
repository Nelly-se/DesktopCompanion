# 数据库基础知识讲解目录

> 这个文件夹专门讲数据库基础、SQLite 数据库是怎么创建出来的，以及 `SpiritDeskDbContext.cs` 这份文件每一段在干什么。

建议阅读顺序：

| 顺序 | 文档 | 你会得到什么 |
|------|------|----------------|
| 1 | [01-数据库基础知识.md](./01-数据库基础知识.md) | 表、行、列、主键、索引、SQLite、连接串等基础概念 |
| 2 | [02-spiritdesk-db是怎么创建出来的.md](./02-spiritdesk-db是怎么创建出来的.md) | `data/spiritdesk.db` 从无到有的完整流程 |
| 3 | [03-SpiritDeskDbContext逐段拆解.md](./03-SpiritDeskDbContext逐段拆解.md) | 逐段讲 `SpiritDeskDbContext.cs` 的 using、DbSet、OnModelCreating、HasData |

---

## 一句话总览

本项目的数据库不是手动提前建好的，而是运行时由 **EF Core + SQLite** 创建：

```text
启动 Web 宿主
  ↓
确定 data/spiritdesk.db 路径
  ↓
注册 SpiritDeskDbContext 使用 SQLite
  ↓
业务服务调用 EnsureCreatedAsync()
  ↓
EF Core 根据 Entity + OnModelCreating 建表
  ↓
HasData 写入五精灵种子数据
  ↓
Service 再补充当前用户档案、演示任务、聊天记录
```

最重要的三个文件：

| 文件 | 作用 |
|------|------|
| `src/SpiritDesk.Web/SpiritDeskWebHost.cs` | 确定数据库路径，注册 EF Core / SQLite |
| `src/SpiritDesk.Web/Data/SpiritDeskDbContext.cs` | 声明表、配置映射、写五精灵种子数据 |
| `src/SpiritDesk.Web/Services/SpiritDeskService.cs` | 调用 `EnsureCreatedAsync()`，触发建库建表，并补充业务初始数据 |

---

## 答辩速背

> 本项目使用 SQLite 作为本地数据库，默认数据库文件是 `data/spiritdesk.db`。启动时 `SpiritDeskWebHost` 会确定数据库路径并注册 `SpiritDeskDbContext`，真正创建数据库发生在 `SpiritDeskService.EnsureInitializedAsync()` 调用 `dbContext.Database.EnsureCreatedAsync()` 时。EF Core 会根据 `Core/Entities` 里的实体类和 `SpiritDeskDbContext.OnModelCreating` 的配置自动建表，并通过 `HasData` 初始化五精灵数据。
