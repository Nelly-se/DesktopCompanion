# SpiritDesk（桌面精灵伴侣）

基于 C# 的 Windows 桌面精灵伴侣应用原型：**WPF 桌面壳**内嵌 **WebView2**，加载本地运行的 **ASP.NET Core Razor Pages** 服务，数据保存在 **SQLite**（EF Core）。

详细产品范围与架构说明见仓库内 [`需求文档.md`](需求文档.md)。

## 功能概览

- 桌面窗口 + 内嵌 Web 界面，本地一体化启动  
- 多精灵设定、任务与成长相关能力（具体以当前实现与需求文档为准）  
- 本地 SQLite 持久化，数据库与密钥目录默认在 Web 进程工作目录下的 `data/`  

## 技术栈

| 模块 | 技术 |
|------|------|
| 桌面壳 | .NET 9、WPF |
| 浏览器内核 | Microsoft WebView2 |
| Web 与业务 | ASP.NET Core、Razor Pages |
| 数据 | EF Core、SQLite |
| 共享逻辑 | `SpiritDesk.Core` 类库 |

## 仓库结构

```text
SpiritDesk.sln
global.json          # 固定本仓库使用 .NET 9 SDK（解析策略见文件内）
需求文档.md
src/
  SpiritDesk.Shell/   # WPF 宿主：拉起 Web 进程并加载 WebView2
  SpiritDesk.Web/     # Razor Pages 站点与业务服务
  SpiritDesk.Core/    # 共享模型与工具
```

## 环境要求

- Windows（WPF / WebView2 目标平台）  
- [.NET 9 SDK](https://dotnet.microsoft.com/download)（与仓库 `TargetFramework` 一致；若出现 NETSDK1045，请安装对应主版本的 SDK）  
- [WebView2 运行时](https://developer.microsoft.com/microsoft-edge/webview2/)（通常已随新版 Edge 安装）  

### 环境说明（NETSDK1045 / 多版本 SDK / NuGet）

- **为什么会出现「已装 9 但仍报不支持 net9」？** 终端里的 `dotnet` 往往是 **`C:\Program Files\dotnet\dotnet.exe`**，它只加载 **`C:\Program Files\dotnet\sdk`** 下的 SDK。若 9 仅装在 `%USERPROFILE%\.dotnet`（例如 install 脚本默认路径），系统上的 `dotnet` **仍只会用 8.x**，从而 NETSDK1045。处理方式二选一：  
  - **推荐**：安装 [.NET 9 SDK Windows x64 安装程序](https://dotnet.microsoft.com/download/dotnet/9.0)到默认目录，再执行 `dotnet --list-sdks` 确认出现 **9.0.x**（与仓库根目录 `global.json` 一致即可）。  
  - **或**：在**当前会话**把用户目录的 dotnet 放到 PATH 最前，并直接调用该 `dotnet.exe`（仅对脚本/临时终端可靠）。  
  - 说明：在多数环境下**不能**仅依赖用户级 `DOTNET_ROOT` 让「Program Files 里的 dotnet」去用户目录找 SDK。  
- 仓库根目录 `NuGet.Config` **仅配置 nuget.org**，请勿再写入某个人电脑上的全局包路径（否则会触发 NU1301 / NU1101）。需要离线源时在个人 `AppData\NuGet\NuGet.Config` 中配置即可。  

## 构建

在仓库根目录执行：

```powershell
dotnet build SpiritDesk.sln -c Debug
```

说明：**`SpiritDesk.Shell` 会按固定路径加载 Web 入口程序集** `src\SpiritDesk.Web\bin\Debug\net9.0\SpiritDesk.Web.dll`。若只做了 `Release` 构建而未生成上述 Debug 输出，壳程序会提示找不到 DLL；课程演示与日常开发建议以 **Debug** 构建为主。

## 运行

### 方式一：推荐 — 启动桌面壳

将 Visual Studio / Rider 的启动项目设为 **`SpiritDesk.Shell`** 后运行，或在命令行：

```powershell
dotnet run --project src\SpiritDesk.Shell\SpiritDesk.Shell.csproj -c Debug
```

壳程序会：

1. 选取本机可用端口，用 `dotnet` 启动 `SpiritDesk.Web`  
2. 等待站点就绪后，在 WebView2 中打开 `http://127.0.0.1:<端口>`  
3. 关闭窗口时结束 Web 子进程  

### 方式二：仅调试 Web（浏览器）

仅开发页面与接口时可单独运行 Web 项目：

```powershell
dotnet run --project src\SpiritDesk.Web\SpiritDesk.Web.csproj
```

默认开发 URL 见 `src/SpiritDesk.Web/Properties/launchSettings.json`（例如 `http://localhost:5160`）。

## 配置说明（对话 / LLM）

`SpiritDesk.Web` 通过 `appsettings.json` 中的 `OpenAI` 节读取兼容 OpenAI Chat Completions 的 HTTP 接口（如自建网关或第三方代理）。**请勿将真实 API 密钥提交到公共仓库**；本地可改用：

- `appsettings.Development.json`（并确保已加入 `.gitignore`），或  
- [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)  

按你实际提供方的要求填写 `ApiKey`、`BaseUrl`、`Model` 等字段。

## 数据文件位置

Web 进程会将 SQLite 与 DataProtection 密钥目录放在**当前内容根**下的 `data/`（具体为 `AppContext.BaseDirectory/data`）。通过壳启动时，工作目录为 `SpiritDesk.Web` 项目目录，数据库文件形如 `.../SpiritDesk.Web/bin/Debug/net9.0/data/spiritdesk.db`（随配置与输出路径可能略有不同）。

## 许可与课程说明

本项目可作为课程/团队项目使用；对外分发或商用前请自行补充许可证与第三方依赖声明。
