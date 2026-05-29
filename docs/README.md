# SpiritDesk 项目文档

> **答辩速成**：先看 [`basic/README.md`](./basic/README.md)（结构、文件夹、话术，不深挖实现）。

本目录为 **SpiritDesk / DesktopCompanion** 的技术说明文档，建议按序号阅读。

## 文档列表

| 目录 | 说明 |
|------|------|
| **[basic/](./basic/)** | 答辩速成：项目结构、文件夹含义、演示与话术（推荐先读） |
| **[basic/auth/](./basic/auth/)** | **登录注册专题**：基础概念、本项目实现、数据库连接、启动踩坑 |
| **[basic/webview2/](./basic/webview2/)** | **WebView2 专题**：嵌入式浏览器、桌面/Web 分工、端口与子进程 |

| 序号 | 文件 | 内容 |
|------|------|------|
| 00 | [00-项目结构与排布.md](./00-项目结构与排布.md) | **学习版**：概念讲解、完整结构树、逐文件说明、自写/生成标注 |
| 01 | [01-CSharp-SpiritDesk.Core.md](./01-CSharp-SpiritDesk.Core.md) | 共享实体、常量、类库职责 |
| 02 | [02-CSharp-Web启动与SpiritDeskWebHost.md](./02-CSharp-Web启动与SpiritDeskWebHost.md) | `Program.cs`、DI、中间件、认证开关 |
| 03 | [03-CSharp-数据层与EF-Core.md](./03-CSharp-数据层与EF-Core.md) | `SpiritDeskDbContext`、种子数据、SQLite |
| 04 | [04-CSharp-业务服务层.md](./04-CSharp-业务服务层.md) | `SpiritDeskService`、人设、LLM |
| 05 | [05-CSharp-Razor-Pages与页面模型.md](./05-CSharp-Razor-Pages与页面模型.md) | 路由、PageModel、各页面职责 |
| 06 | [06-CSharp-认证与登录注册.md](./06-CSharp-认证与登录注册.md) | Cookie 认证、登录注册流程 |
| 07 | [07-CSharp-Shell桌面宿主.md](./07-CSharp-Shell桌面宿主.md) | WPF、WebView2、桌面浮球 |
| 08 | [08-API与静态资源.md](./08-API与静态资源.md) | Minimal API、`wwwroot`、主题 CSS |
| 09 | [09-构建运行与部署.md](./09-构建运行与部署.md) | 本地运行、发布、云端部署 |
| 10 | [10-SpiritDesk.Web项目与cshtml详解.md](./10-SpiritDesk.Web项目与cshtml详解.md) | **Web csproj、SSR、每个 .cshtml 职责** |
| 11 | [11-登录注册流程专项说明.md](./11-登录注册流程专项说明.md) | **专项说明**：配置、注册、登录、登出、账号档案关系 |
| 12 | [12-CSharp-Helpers工具类详解.md](./12-CSharp-Helpers工具类详解.md) | **Helpers 专项说明**：`.env`、时间问候语、SQLite 启动校验 |
| 13 | [13-Program入口与桌面Web运行原理.md](./13-Program入口与桌面Web运行原理.md) | **入口与运行原理**：桌面 Shell、Web 服务、顶级语句、时序图 |
| 14 | [14-WPF-App.xaml与App.xaml.cs详解.md](./14-WPF-App.xaml与App.xaml.cs详解.md) | **WPF 入口专项**：`App.xaml.cs`、`:行号`、XAML 与代码后置 |
| basic/09 | [basic/09-MVC还是MVVM我们用的哪种.md](./basic/09-MVC还是MVVM我们用的哪种.md) | **MVC vs MVVM**、Razor Pages、ViewModel 命名含义 |

## 其他材料

- [朱雅轩-实验报告.md](./朱雅轩-实验报告.md) — 课程实验报告
- 仓库根目录 [README.md](../README.md) — 快速命令
- [需求文档.md](../需求文档.md) — 功能与验收流程
- [deploy/aliyun/DEPLOYMENT.md](../deploy/aliyun/DEPLOYMENT.md) — 服务器部署

## 技术栈速查

| 类别 | 技术 |
|------|------|
| 运行时 | .NET 9 |
| Web | ASP.NET Core Razor Pages、Minimal API |
| 数据 | EF Core、SQLite |
| 桌面 | WPF、WebView2 |
| 认证 | Cookie（可选） |
| 设计资源 | Figma → PNG → `wwwroot/assets` |
| 大模型 | 豆包/方舟兼容 API（`.env` / `ARK_*`） |

## 当前代码版本

文档基于 `main` 分支同步编写。拉取远程后若结构有变，以仓库实际文件为准。
