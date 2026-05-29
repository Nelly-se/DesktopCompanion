# SpiritDesk 入门讲解文档（答辩向）

> 目标：**快速搞懂项目怎么组织的**，能在老师面前讲清楚「这是什么、分几块、文件夹干什么」。  
> 不要求你会写每一行代码。

## 建议阅读顺序（约 30～45 分钟）

| 顺序 | 文档 | 你会得到什么 |
|------|------|----------------|
| 1 | [01-项目是干什么的.md](./01-项目是干什么的.md) | 一句话定位 + 和老师怎么开场 |
| 2 | [02-整体结构一张图.md](./02-整体结构一张图.md) | 三个项目、谁连谁 |
| 3 | [03-根目录文件夹说明.md](./03-根目录文件夹说明.md) | 仓库里每个顶层文件夹 |
| 4 | [04-src里面每个项目.md](./04-src里面每个项目.md) | `Core` / `Web` / `Shell` 里常见文件夹 |
| 4a | [csharp/README.md](./csharp/README.md) | **C# 基本语法与数据结构**：变量、类、方法、List、Dictionary、async/LINQ |
| 4b | [12-csproj与obj文件夹详解.md](./12-csproj与obj文件夹详解.md) | **`.csproj` 工程文件** + **`obj`/`bin` 编译目录** |
| 4c | [13-EF是什么详解.md](./13-EF是什么详解.md) | **EF / EF Core** 是什么、在本项目里怎么用 |
| 4d | [14-数据库文件与迁移详解.md](./14-数据库文件与迁移详解.md) | **`spiritdesk.db` 为何不进 Git**、要不要 migration、和 TypeORM 对照 |
| 4e | [16-数据访问层与对象关系映射详解.md](./16-数据访问层与对象关系映射详解.md) | **数据访问层 + ORM**：解释“EF Core 进行对象关系映射”这句话 |
| 4f | [17-DbContext是什么详解.md](./17-DbContext是什么详解.md) | **DbContext 专讲**：数据库上下文、DbSet、SaveChangesAsync |
| 4g | [database/README.md](./database/README.md) | **数据库基础专题**：数据库怎么创建、SQLite 基础、DbContext 文件逐段拆解 |
| **★** | **[auth/](./auth/README.md)** | **登录注册专题文件夹**：基础概念 → 本项目实现 → 连数据库 → 踩坑 |
| **★** | **[middleware/](./middleware/README.md)** | **中间件专题**：管道基础、`SpiritDeskWebHost` 里 Use/Map 顺序、四条请求路径 |
| **★** | **[webview2/](./webview2/README.md)** | **WebView2 专题**：是什么、前后端分工、端口与子进程、答辩话术 |
| **★** | [15-登录注册功能详解.md](./15-登录注册功能详解.md) | **答辩专篇**：登录/注册/Cookie/配置开关/演示脚本 |
| 5 | [05-重要文件速查表.md](./05-重要文件速查表.md) | 只记十几个文件名就够 |
| 6 | [06-功能与页面对照.md](./06-功能与页面对照.md) | 演示时点什么、对应哪个页面 |
| 7 | [07-老师提问怎么答.md](./07-老师提问怎么答.md) | 常见问题标准答法 |
| 8 | [08-wwwroot与静态资源详解.md](./08-wwwroot与静态资源详解.md) | **wwwroot 专篇**：静态 CSS/JS/图片 vs SSR |
| 8b | [11-cshtml与wwwroot一次请求如何生成页面.md](./11-cshtml与wwwroot一次请求如何生成页面.md) | **图文并茂**：打开首页时 SSR 与静态资源各走哪条路 |
| 9 | [09-MVC还是MVVM我们用的哪种.md](./09-MVC还是MVVM我们用的哪种.md) | **MVC / MVVM / Razor Pages** 对照与答辩答法 |
| 10 | [10-MVC与MVVM从零讲起.md](./10-MVC与MVVM从零讲起.md) | **零基础**：MVC/MVVM 是什么、前后端、和本项目区别 |
| — | [10-SpiritDesk.Web项目与cshtml详解.md](../10-SpiritDesk.Web项目与cshtml详解.md) | **Web csproj + 每个 cshtml + 是否 SSR** |

## 和 `docs/` 里其他文档的区别

| 目录 | 适合谁 |
|------|--------|
| **`docs/basic/`（本目录）** | 答辩前速成、讲结构 |
| [00-项目结构与排布](../00-项目结构与排布.md) | **推荐**：零基础搞懂结构、WPF/Razor/EF/API |
| `docs/01～09` | 想深入 C# 实现时再读 |

## 你负责的部分（答辩可强调）

- 五精灵性格与 Figma 形象、切图  
- 登录 / 注册页面 + 后端登录逻辑  
- 全站薄荷色配色与布局优化  

讲法见 [07-老师提问怎么答.md](./07-老师提问怎么答.md) 第四节。
