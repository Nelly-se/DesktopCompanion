# SpiritDesk

基于 C# 的桌面精灵伴侣 Agent 应用。

**启动命令速查** → 见根目录 **[启动说明.md](./启动说明.md)**（含是否登录、答辩演示账号）。

## 项目定位

SpiritDesk 是一个“桌面精灵伴侣 Agent”项目，不是单纯网页系统。

- 本地桌面版：`SpiritDesk.Shell`（WPF + WebView2）
- Web 版（本地/云端）：`SpiritDesk.Web`（ASP.NET Core Razor Pages）

## 技术栈

- WPF
- WebView2
- ASP.NET Core Razor Pages
- EF Core
- SQLite

## 完整源代码目录

> 课程大作业提交源代码时，以本仓库 **Git 跟踪的文件** 为准。  
> 下列目录 **不包含** 本地编译/运行产物：`bin/`、`obj/`、`artifacts/`、`.vs/`、`.env`（含 API 密钥）。

```
DesktopCompanion/                          # 仓库根目录
│
├── SpiritDesk.sln                         # 解决方案（Core + Web + Shell 三项目）
├── global.json                            # .NET SDK 版本约束
├── NuGet.Config                           # NuGet 包源配置
├── .gitignore                             # Git 忽略规则
├── .env.example                           # 大模型 API 配置示例（复制为 .env 后本地使用）
│
├── README.md                              # 项目说明（本文件）
├── 启动说明.md                             # 启动方式与演示账号
├── 需求文档.md                             # 功能需求与验收流程
│
├── build.ps1                              # 编译整个解决方案
├── run-shell.ps1                          # 桌面版（免登录）
├── run-shell-with-auth.ps1                # 桌面版（含登录注册演示）
├── run-web.ps1                            # 仅浏览器网页版
│
├── .vscode/                               # VS Code 调试配置（可选）
│   ├── launch.json
│   └── tasks.json
│
├── deploy/                                # 云端部署（仅发布 SpiritDesk.Web）
│   ├── aliyun/
│   │   ├── DEPLOYMENT.md                  # 阿里云部署说明
│   │   └── spiritdesk.env.example         # 服务器环境变量示例
│   ├── nginx/
│   │   └── spiritdesk.conf                # Nginx 反向代理
│   ├── systemd/
│   │   └── spiritdesk-web.service         # systemd 服务单元
│   └── scripts/
│       ├── publish-web.ps1                # 发布 Web 到 artifacts/
│       └── upload-spiritdesk-web.py       # 上传发布包到服务器
│
├── docs/                                  # 项目文档与学习笔记
│   ├── README.md
│   ├── 00-项目结构与排布.md
│   ├── 01-CSharp-SpiritDesk.Core.md
│   ├── 02-CSharp-Web启动与SpiritDeskWebHost.md
│   ├── 03-CSharp-数据层与EF-Core.md
│   ├── 04-CSharp-业务服务层.md
│   ├── 05-CSharp-Razor-Pages与页面模型.md
│   ├── 06-CSharp-认证与登录注册.md
│   ├── 07-CSharp-Shell桌面宿主.md
│   ├── 08-API与静态资源.md
│   ├── 09-构建运行与部署.md
│   ├── 10-SpiritDesk.Web项目与cshtml详解.md
│   ├── 11-登录注册流程专项说明.md
│   ├── 12-CSharp-Helpers工具类详解.md
│   ├── 13-Program入口与桌面Web运行原理.md
│   ├── 14-WPF-App.xaml与App.xaml.cs详解.md
│   ├── PPT-系统介绍写作提纲.md
│   ├── 项目汇总-架构类关系与交互流程.md
│   ├── 朱雅轩-实验报告.md
│   └── basic/                             # 零基础入门文档
│       ├── README.md
│       ├── 000-ASP.NET学习.md
│       ├── 01-项目是干什么的.md
│       ├── 02-整体结构一张图.md
│       ├── 03-根目录文件夹说明.md
│       ├── 04-src里面每个项目.md
│       ├── 05-重要文件速查表.md
│       ├── 06-功能与页面对照.md
│       ├── 07-老师提问怎么答.md
│       ├── 08-wwwroot与静态资源详解.md
│       ├── 09-MVC还是MVVM我们用的哪种.md
│       ├── 10-MVC与MVVM从零讲起.md
│       ├── 11-cshtml与wwwroot一次请求如何生成页面.md
│       ├── 12-csproj与obj文件夹详解.md
│       ├── 13-EF是什么详解.md
│       ├── 14-数据库文件与迁移详解.md
│       ├── 15-登录注册功能详解.md
│       ├── 16-数据访问层与对象关系映射详解.md
│       ├── 17-DbContext是什么详解.md
│       ├── auth/
│       │   ├── README.md
│       │   ├── 01-登录注册基础知识.md
│       │   ├── 02-本项目登录注册怎么做.md
│       │   ├── 03-登录注册如何连接数据库.md
│       │   ├── 04-不同启动方式与常见踩坑.md
│       │   ├── 05-Cookie四个概念详解.md
│       │   ├── 06-HttpOnly与XSS详解.md
│       │   └── 07-登录注册代码全景与逐行导读.md
│       ├── csharp/
│       │   ├── README.md
│       │   ├── 01-基本语法.md
│       │   ├── 02-方法与流程控制.md
│       │   ├── 03-类对象与项目常见写法.md
│       │   ├── 04-常用数据结构.md
│       │   └── 05-异步LINQ与答辩速查.md
│       ├── database/
│       │   ├── README.md
│       │   ├── 01-数据库基础知识.md
│       │   ├── 02-spiritdesk-db是怎么创建出来的.md
│       │   └── 03-SpiritDeskDbContext逐段拆解.md
│       ├── middleware/
│       │   ├── README.md
│       │   ├── 01-中间件基础知识.md
│       │   ├── 02-本项目中间件管道详解.md
│       │   └── 03-一次请求如何穿过中间件.md
│       └── webview2/
│           ├── README.md
│           ├── 01-WebView2是什么.md
│           ├── 02-前端Web与桌面端分工.md
│           ├── 03-本项目WebView2怎么用.md
│           ├── 04-端口Cookie与本地Web子进程.md
│           └── 05-常见方案对比与答辩话术.md
│
├── temp/                                  # 临时辅助工具（非主程序）
│   └── TempCheckDb/
│       ├── TempCheckDb.csproj
│       └── Program.cs
│
└── src/                                   # ★ 全部 C# 源代码
    │
    ├── SpiritDesk.Core/                   # 核心实体与常量（类库）
    │   ├── SpiritDesk.Core.csproj
    │   ├── Constants/
    │   │   └── SpiritIds.cs               # 五精灵 ID 常量
    │   └── Entities/
    │       ├── UserProfile.cs             # 用户养成档案
    │       ├── SpiritDefinition.cs        # 精灵元数据
    │       ├── TaskItem.cs                # 待办任务
    │       ├── ChatMessage.cs             # 聊天记录
    │       ├── DailyActionLog.cs          # 每日互动日志
    │       └── WebAccount.cs              # 登录账号
    │
    ├── SpiritDesk.Web/                    # Web 端与业务服务（ASP.NET Core）
    │   ├── SpiritDesk.Web.csproj
    │   ├── Program.cs                     # 入口，转 SpiritDeskWebHost
    │   ├── SpiritDeskWebHost.cs           # 启动管道、DI、Razor、API
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── Dockerfile
    │   ├── .dockerignore
    │   ├── Properties/
    │   │   └── launchSettings.json
    │   ├── Auth/
    │   │   ├── SpiritDeskAuthHelper.cs
    │   │   └── WebAccountAuth.cs
    │   ├── Data/
    │   │   └── SpiritDeskDbContext.cs     # EF Core 数据库上下文
    │   ├── Helpers/
    │   │   ├── DatabaseBootstrapper.cs
    │   │   ├── DateTimeHelper.cs
    │   │   └── DotEnvLoader.cs
    │   ├── Models/
    │   │   ├── SpiritDeskViewModel.cs
    │   │   ├── ChatHistoryViewModel.cs
    │   │   ├── SettingsViewModel.cs
    │   │   ├── TaskSnapshotViewModel.cs
    │   │   ├── SpiritSelectionResult.cs
    │   │   ├── OperationFeedback.cs
    │   │   └── CompanionApiModels.cs
    │   ├── Services/
    │   │   ├── SpiritDeskService.cs       # 核心业务
    │   │   ├── SpiritPersonaService.cs    # 五精灵话术
    │   │   └── LlmReplyService.cs         # 大模型调用
    │   ├── Pages/
    │   │   ├── _ViewImports.cshtml
    │   │   ├── _ViewStart.cshtml
    │   │   ├── Index.cshtml / Index.cshtml.cs           # 首页工作台
    │   │   ├── Chat.cshtml / Chat.cshtml.cs             # 精灵聊天
    │   │   ├── Assistant.cshtml / Assistant.cshtml.cs   # 猜拳小游戏
    │   │   ├── Settings.cshtml / Settings.cshtml.cs     # 设置
    │   │   ├── Login.cshtml / Login.cshtml.cs           # 登录
    │   │   ├── Register.cshtml / Register.cshtml.cs     # 注册
    │   │   ├── Logout.cshtml / Logout.cshtml.cs         # 登出
    │   │   ├── Error.cshtml / Error.cshtml.cs
    │   │   ├── Privacy.cshtml / Privacy.cshtml.cs
    │   │   ├── Spirits/
    │   │   │   └── Select.cshtml / Select.cshtml.cs     # 五选一选精灵
    │   │   └── Shared/
    │   │       ├── _Layout.cshtml / _Layout.cshtml.css  # 全站布局
    │   │       ├── _FloatingCompanion.cshtml            # 网页内悬浮精灵
    │   │       ├── _SidebarCollapseToggle.cshtml
    │   │       └── _ValidationScriptsPartial.cshtml
    │   └── wwwroot/                       # 静态资源
    │       ├── favicon.ico
    │       ├── css/
    │       │   └── site.css               # 主样式（薄荷主题）
    │       ├── js/
    │       │   ├── site.js
    │       │   └── desk-calendar.js
    │       ├── assets/images/             # 精灵与背景图
    │       │   ├── bubble-bg.png
    │       │   ├── pc-background.png
    │       │   ├── spirit-light.png       # 卷卷晴
    │       │   ├── spirit-water.png       # 嘻嘻滴
    │       │   ├── spirit-air.png         # 贴贴朵
    │       │   ├── spirit-soil.png        # 慢慢壤
    │       │   └── spirit-nutrition.png   # 新新星
    │       └── lib/                       # 第三方前端库（dotnet new 模板自带）
    │           ├── bootstrap/             # Bootstrap CSS/JS
    │           ├── jquery/
    │           ├── jquery-validation/
    │           └── jquery-validation-unobtrusive/
    │
    └── SpiritDesk.Shell/                  # Windows 桌面壳（WPF + WebView2）
        ├── SpiritDesk.Shell.csproj
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / MainWindow.xaml.cs       # 主窗口（内嵌 WebView2）
        ├── CompanionBubbleWindow.xaml / .cs           # 系统级桌面浮球
        ├── ShellSettings.cs
        ├── ShellSettingsService.cs
        └── AssemblyInfo.cs
```

### 目录职责速查

| 路径 | 说明 |
|------|------|
| `src/SpiritDesk.Core` | 实体类与精灵常量，被 Web / Shell 共同引用 |
| `src/SpiritDesk.Web` | Razor 页面、EF Core、SQLite、业务服务、Companion API |
| `src/SpiritDesk.Shell` | WPF 桌面宿主与系统级浮球，通过 HTTP 调用 Web |
| `deploy/` | 阿里云 / Nginx / systemd 部署配置与脚本 |
| `docs/` | 架构说明、答辩笔记与零基础教程 |
| `run-*.ps1` | 本地一键启动脚本 |

运行后会在本地生成（**无需提交**）：`src/SpiritDesk.Web/data/spiritdesk.db`（SQLite 数据库）、各项目下 `bin/` 与 `obj/`、`artifacts/spiritdesk-web-publish/`（发布包）。

## 本地构建

```powershell
dotnet build SpiritDesk.sln
```

## 本地 LLM 配置（豆包 / 方舟）

项目支持直接读取仓库根目录或 `src/SpiritDesk.Web` 目录下的 `.env` 文件。

1. 复制 `.\.env.example` 为 `.\.env`
2. 填入你的真实 `ARK_API_KEY`
3. 保持 `ARK_API_BASE=https://ark.cn-beijing.volces.com/api/v3`
4. 保持 `ARK_MODEL=doubao-seed-2-0-lite-260215`

`.env` 已被 `.gitignore` 忽略，不会默认提交。

## 运行方式

详见 **[启动说明.md](./启动说明.md)**。常用三条：


| 场景              | 命令                          |
| --------------- | --------------------------- |
| 桌面 + 浮球，免登录     | `.\run-shell.ps1`           |
| 桌面 + **演示登录注册** | `.\run-shell-with-auth.ps1` |
| 仅浏览器网页          | `.\run-web.ps1`             |


## 云端与本地职责边界

- 阿里云上只部署 `SpiritDesk.Web`
- 本机 Windows 上运行 `SpiritDesk.Shell`
- 浏览器直接访问 `http://116.62.19.40` 只能看到网页内悬浮精灵
- 真正系统级桌面浮球必须通过 WPF Shell 运行

## 答辩演示流程（建议）

1. 首次建档
2. 五选一精灵（卷卷晴 / 嘻嘻滴 / 贴贴朵 / 慢慢壤 / 新新星）
3. 进入首页（精灵主页）
4. 精灵聊天
5. 任务新增 / 编辑 / 删除 / 完成
6. 每日签到 / 投喂 / 互动
7. 猜拳小游戏
8. 重启后验证 SQLite 数据保留

## 说明

- 未配置大模型 API 时，系统会使用规则兜底/演示脚本，保证离线可演示。
- 登录注册为可配置演示门禁（见 `docs/basic/15-登录注册功能详解.md`）；养成档案仍为单用户演示级设计。

