# 04 - src 里面每个项目

所有业务代码在 **`src/`** 下，只有三个子项目。

---

## SpiritDesk.Core（小字典）

**作用**：定义「数据长什么样」，不写页面、不连数据库。

| 文件夹/文件 | 含义 |
|-------------|------|
| **`Entities/`** | 实体类：用户档案、任务、聊天记录、账号等 |
| **`Constants/SpiritIds.cs`** | 五个精灵的 ID 常量（代码里统一用字符串） |

**答辩怎么说**：Core 是**共享的数据模型**，Web 和以后别的模块都引用它，避免重复定义。

**不用背的**：每个 `.cs` 里具体字段名，知道「用户、任务、聊天」三类就够。

---

## SpiritDesk.Web（大脑 + 网页）

**作用**：用户看到的页面、保存数据、调用大模型、给桌面浮球提供 API。

### `SpiritDesk.Web.csproj` 到底是啥？

| 问题 | 答案 |
|------|------|
| 它是什么？ | **网站工程文件**，SDK 为 `Microsoft.NET.Sdk.Web`（不是普通类库） |
| 编译出什么？ | `SpiritDesk.Web.dll` + 复制 `wwwroot/` + Razor 页面编译进 DLL |
| 引用谁？ | `SpiritDesk.Core`（实体）；NuGet：EF Sqlite、密码哈希、健康检查 |
| 谁运行它？ | `dotnet run` 开浏览器；或 **Shell 的 WebView2** 内嵌同一站点 |
| 和 Shell 关系？ | Shell **不重复业务**，只嵌这个 Web；浮球再调 Web 的 `/api/companion/*` |

**答辩**：Web 项目 = **Razor SSR 网页 + SQLite + 可选登录 + 静态资源目录 wwwroot + 浮球 API**。

> `.csproj` / `obj` / `bin` 通用说明见 [12-csproj与obj文件夹详解.md](./12-csproj与obj文件夹详解.md)。

### `.cshtml` 是干什么的？是 SSR 吗？

- **是 SSR**：`.cshtml` 在**服务器**上变成 HTML，浏览器收到已填好数据的页面。
- **不是** React/Vue 那种「空 HTML + 前端框架拉 API」。
- 每个业务页通常 **一对文件**：`Xxx.cshtml`（长什么样）+ `Xxx.cshtml.cs`（查库、处理表单）。
- 专篇：[10-SpiritDesk.Web项目与cshtml详解.md](../10-SpiritDesk.Web项目与cshtml详解.md)（每个 cshtml 表格 + 流程图）。

### 文件夹地图

```
SpiritDesk.Web/
├── Pages/           ← 每个功能一页（Razor Pages）
├── Services/        ← 业务逻辑（任务、聊天、精灵人设等）
├── Data/            ← 数据库上下文（EF Core + SQLite）
├── Models/          ← 页面用的表单/展示模型（不是数据库表）
├── Auth/            ← 登录门禁相关小工具
├── Helpers/         ← 读 .env 等辅助
├── wwwroot/         ← 静态资源（CSS、JS、精灵 PNG）
├── appsettings.json ← 配置（端口、是否开登录等）
├── Program.cs       ← 入口（很短，真正启动在 SpiritDeskWebHost.cs）
└── SpiritDeskWebHost.cs  ← 注册服务、路由、API
```

### Pages/ 里常见页面（和功能对应）

| 文件 | 用户看到什么 |
|------|----------------|
| `Login.cshtml` | 登录 |
| `Register.cshtml` | 注册 |
| `Spirits/Select.cshtml` | 五选一选精灵 |
| `Index.cshtml` | 首页（任务、日历、猜拳入口） |
| `Chat.cshtml` | 和精灵聊天 |
| `Assistant.cshtml` | 任务助手（签到、投喂等） |
| `Settings.cshtml` | 设置 |
| `Shared/_Layout.cshtml` | 整站外壳（侧栏、导航） |
| `Shared/_FloatingCompanion.cshtml` | 网页里右下角悬浮小精灵 |

每个页面通常有一对文件：

- **`.cshtml`**：长什么样（HTML）
- **`.cshtml.cs`**：点按钮后干什么（C# 代码）

### Services/（业务在谁手里）

| 典型文件 | 干什么（一句话） |
|----------|------------------|
| `SpiritDeskService.cs` | 总业务：建档、任务、签到、选精灵 |
| `LlmReplyService.cs` | 调大模型生成回复 |
| `SpiritPersonaService.cs` | 五个精灵不同说话风格 |

**答辩怎么说**：页面只负责展示，**复杂逻辑放在 Services**，这样好维护。

### Data/（EF Core）

| 典型文件 | 干什么 |
|----------|--------|
| `SpiritDeskDbContext.cs` | **EF Core** 数据库上下文：C# 实体 ↔ SQLite 表 |
| 数据库文件 | 运行后生成 `data/spiritdesk.db`（EF 读写） |

**EF 是什么？** 见 [13-EF是什么详解.md](./13-EF是什么详解.md)；配置与种子见 [03-CSharp-数据层与EF-Core.md](../03-CSharp-数据层与EF-Core.md)。

### wwwroot/（静态资源，不是 SSR 页面）

**和 `.cshtml` 分工**：cshtml = 动态 HTML（数据来自数据库）；wwwroot = 固定 CSS/JS/图片，URL 不变。

| 路径 | 干什么 |
|------|--------|
| **`css/site.css`** | 全站样式、薄荷色主题（你改过） |
| **`assets/images/`** | 精灵 PNG、背景图（Figma 导出） |
| **`js/`** | 侧栏折叠、猜拳弹窗、日历等少量脚本 |
| **`lib/`** | Bootstrap、jQuery（模板自带，一般不用改） |

**专篇（建议单独读）**：[08-wwwroot与静态资源详解.md](./08-wwwroot与静态资源详解.md)

### 给 Shell 用的 API（知道有就行）

在 `SpiritDeskWebHost.cs` 里，例如：

- `GET /api/companion/current` — 当前精灵信息  
- `POST /api/companion/select-spirit` — 切换精灵  

浮球右键换精灵会调这些接口。

---

## SpiritDesk.Shell（Windows 外壳）

**作用**：不是「再写一套业务」，而是**在 Windows 上开窗口 + 浮球**，里面显示 Web 页面。

| 文件 | 干什么 |
|------|--------|
| `MainWindow.xaml` / `.cs` | 主窗口，内嵌 WebView2 浏览器 |
| `CompanionBubbleWindow.xaml` / `.cs` | **桌面浮球**（可拖、右键换精灵） |
| `App.xaml` / `.cs` | 程序启动入口 |
| `ShellSettings.cs` | 记住窗口位置等 |
| `ShellSettingsService.cs` | 读写本地设置 |

**答辩怎么说**：Shell 是 **WPF 桌面壳**，业务仍由 Web 项目提供；浮球通过 HTTP 问 Web「现在是谁」。

---

## 三个项目对照表（打印这一张）

| 项目 | 类型 | 一句话 |
|------|------|--------|
| Core | 类库 | 数据模型 + 精灵 ID |
| Web | 网站 | 页面 + 数据库 + AI + API |
| Shell | 桌面 exe | 窗口 + 浮球，嵌 Web |

下一篇：[05-重要文件速查表.md](./05-重要文件速查表.md)
