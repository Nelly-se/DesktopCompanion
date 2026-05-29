# 12 - .csproj 与 obj 文件夹详解

> 打开 `src/` 时，每个子项目旁边都有 **`XXX.csproj`**，编译后还会出现 **`bin/`、`obj/`**。  
> 本篇说明它们是什么、和 `.sln` 有何区别、要不要提交 Git、答辩怎么答。

---

## 0. 一句话对照（先背这张表）

| 名字 | 是什么 | 谁维护 | 要不要进 Git |
|------|--------|--------|--------------|
| **`SpiritDesk.sln`** | 解决方案：把多个 `.csproj` 编进一个 VS 窗口 | 人手改（很少） | ✅ 要 |
| **`SpiritDesk.Web.csproj`** 等 | **单个项目**的「说明书 + 依赖清单」 | 人手改（加包、加引用时） | ✅ 要 |
| **`obj/`** | 编译**过程**产生的中间文件、缓存 | `dotnet build` **自动生成** | ❌ 不要（已在 `.gitignore`） |
| **`bin/`** | 编译**结果**（`.dll`、`.exe`、`wwwroot` 复制等） | `dotnet build` / `dotnet run` 自动生成 | ❌ 不要 |

**答辩一句话**：`.csproj` 描述「这个项目怎么编、依赖谁」；`obj` 是编译临时目录，可随时删了重新 `dotnet build` 生成。

---

## 1. `.csproj` 是干什么的？

### 1.1 本质

`.csproj` = **C# 项目工程文件**（XML），告诉 **MSBuild / `dotnet` CLI**：

1. 这是**类库**还是**网站**还是**桌面 exe**  
2. 目标框架是 **.NET 9** 还是 **net9.0-windows（WPF）**  
3. 要编译哪些 `.cs`、`.xaml`、`.cshtml`  
4. 要引用哪些 **NuGet 包**、哪些**本仓库其它项目**  
5. 编译后输出叫什么（例如 `SpiritDesk.Web.dll`）

可以把它想成 **菜谱**：你写的 `.cs` 是食材，`.csproj` 是「怎么做这道菜、还要借隔壁项目的料」。

### 1.2 和 `.sln` 的区别

```
SpiritDesk.sln          ← 「一桌菜」：列出 Core、Web、Shell 三个项目
    ├── SpiritDesk.Core.csproj
    ├── SpiritDesk.Web.csproj
    └── SpiritDesk.Shell.csproj
```

| 文件 | 管什么 |
|------|--------|
| **`.sln`** | 多个项目怎么一起在 Visual Studio 里打开、一起编译 |
| **`.csproj`** | **某一个项目**自己的框架、包、引用、输出类型 |

日常改业务代码改的是 `.cs`；**加 NuGet 包、让 Web 引用 Core** 时才会动 `.csproj`。

### 1.3 文件长什么样（常见块）

以本仓库为例，一个 `.csproj` 里通常有这些块：

```xml
<!-- 第一行：用哪种 SDK 模板（决定默认能力） -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <!-- 项目引用：引用本仓库里的另一个 .csproj -->
  <ItemGroup>
    <ProjectReference Include="..\SpiritDesk.Core\SpiritDesk.Core.csproj" />
  </ItemGroup>

  <!-- NuGet 包：从 nuget.org 下载的第三方库 -->
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.5" />
  </ItemGroup>

  <!-- 属性：框架版本、是否可空、输出是 exe 还是 dll -->
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

| 标签 | 含义 | 本项目例子 |
|------|------|------------|
| `Sdk="Microsoft.NET.Sdk"` | 普通**类库** | `SpiritDesk.Core` |
| `Sdk="Microsoft.NET.Sdk.Web"` | **ASP.NET Core 网站**（自带 Razor、wwwroot 规则） | `SpiritDesk.Web` |
| `Sdk="Microsoft.NET.Sdk"` + `<UseWPF>true</UseWPF>` | **WPF 桌面** | `SpiritDesk.Shell` |
| `<ProjectReference>` | 编译前先编被引用项目，运行时要带上对方 DLL | Web → Core；Shell → Core + Web |
| `<PackageReference>` | 从 NuGet 拉 EF、WebView2 等 | Web 的 Sqlite；Shell 的 WebView2 |
| `<TargetFramework>` | 用哪个 .NET 版本 | `net9.0` / `net9.0-windows` |
| `<OutputType>WinExe</OutputType>` | 输出 Windows 可执行文件 | 仅 Shell |

### 1.4 本仓库三个 `.csproj` 各干什么

| 项目文件 | Sdk / 关键属性 | 编译出什么 | 引用关系 |
|----------|----------------|------------|----------|
| `SpiritDesk.Core.csproj` | `Microsoft.NET.Sdk`，`net9.0` | **类库** `SpiritDesk.Core.dll`（只有实体、常量） | 无项目引用 |
| `SpiritDesk.Web.csproj` | `Microsoft.NET.Sdk.Web`，`net9.0` | **网站** `SpiritDesk.Web.dll` + Razor 编译 + 复制 `wwwroot` | 引用 **Core**；NuGet：EF Sqlite 等 |
| `SpiritDesk.Shell.csproj` | `Microsoft.NET.Sdk` + WPF，`net9.0-windows`，`WinExe` | **桌面 exe** `SpiritDesk.Shell.exe` | 引用 **Core** 和 **Web**（保证跑 Shell 时 Web 也会重新编译） |

**和 `04-src里面每个项目.md` 的关系**：那一篇讲文件夹里**业务代码**；本篇讲**工程文件**如何把那些代码变成可运行的 DLL/exe。

### 1.5 什么时候你会改 `.csproj`？

| 场景 | 典型操作 |
|------|----------|
| Visual Studio 里「管理 NuGet 包」 | 自动往 `.csproj` 里加一行 `<PackageReference>` |
| 右键「添加项目引用」 | 自动加 `<ProjectReference>` |
| 新建 `dotnet new classlib` / `webapp` | 生成新的 `.csproj` |
| 很少手写 | 改 `TargetFramework`、改输出类型等 |

**答辩**：业务逻辑在 `.cs`；`.csproj` 是**工程和依赖配置**，一般通过 IDE 维护，不是每天改的业务文件。

---

## 2. `obj/` 文件夹是干什么的？

### 2.1 本质

`obj/` = **Objective / 中间输出目录**（习惯叫 obj）。  
每次 `dotnet build`、`dotnet run` 时，编译器会把**还没变成最终程序**的东西放在这里，例如：

| 常见内容 | 作用 |
|----------|------|
| `*.csproj.nuget.*` | NuGet 还原结果、依赖图缓存 |
| `project.assets.json` | 这个项目解析到的全部包和引用 |
| `*.dll`（中间版） | 某次编译的中间程序集 |
| Razor / XAML 生成代码 | 把 `.cshtml`、`.xaml` 先变成 `.cs` 再编译 |
| `SpiritDesk.Web.GeneratedMSBuildEditorConfig.editorconfig` 等 | 工具生成的辅助配置 |

**特点**：删了整个 `obj/` 文件夹，再执行 `dotnet build`，一般会**重新生成**（第一次可能稍慢，因为要重新还原包、重新生成代码）。

### 2.2 和 `bin/` 一起记（老师常连着问）

每个项目目录下通常成对出现：

```
SpiritDesk.Web/
├── SpiritDesk.Web.csproj    ← 你维护的工程文件
├── Pages/、Services/ ...     ← 你维护的源代码
├── obj/                      ← 编译过程临时（可删）
└── bin/
    └── Debug/net9.0/
        ├── SpiritDesk.Web.dll    ← 最终要运行的程序集
        ├── SpiritDesk.Web.deps.json
        └── ...（依赖 DLL、静态文件复制等）
```

| 目录 | 阶段 | 比喻 |
|------|------|------|
| **`obj/`** | 炒菜过程中的切菜盘、半成品 | 厨房临时台面 |
| **`bin/`** | 装盘可以端出去的菜 | 成品 |

`dotnet run` 默认会去 **`bin/Debug/...`**（或 Release）里启动程序，而不是直接跑 `obj/` 里的半成品。

### 2.2.1 `bin/` 是怎么生成的？

**谁触发**：只要对某个 `.csproj` 做「编译或运行」，就会在**该项目目录下**自动创建/更新 `bin/`。

| 你做的操作 | 背后发生的事 |
|------------|----------------|
| `dotnet build` | MSBuild 按 `.csproj` 编译 → 输出写入 `bin/Debug/<框架>/`（或 `Release`） |
| `dotnet run`（在 Web/Shell 目录） | 先 build（若无更新可能跳过），再从 `bin/...` 启动 |
| Visual Studio 按 F5 | 等价于 build + run |
| 根目录 `build.ps1` | 编译整个解决方案，三个项目的 `bin` 都会更新 |
| `dotnet publish` | 除了 `bin`，还会在 `artifacts/` 等目录生成**部署用**的更完整拷贝（见下文） |

**路径规则**（本项目）：

```
src/SpiritDesk.Core/bin/Debug/net9.0/
src/SpiritDesk.Web/bin/Debug/net9.0/
src/SpiritDesk.Shell/bin/Debug/net9.0-windows/
```

- 第一层 **`Debug` / `Release`**：配置名（调试版体积大、带 `.pdb`；发布版可优化）  
- 第二层 **`net9.0` / `net9.0-windows`**：来自 `.csproj` 里的 `<TargetFramework>`

**和 `obj/` 的分工**：编译先在 `obj` 里生成中间结果，再把**最终要运行的文件**复制/链接到 `bin`。你只关心「跑哪个程序」时，看 `bin` 即可。

### 2.2.2 `bin/` 里面都是什么？（按三类记）

#### ① 你自己项目的成品

| 文件 | 含义 |
|------|------|
| `SpiritDesk.Core.dll` | Core 类库编译结果 |
| `SpiritDesk.Web.dll` / `SpiritDesk.Web.exe` | Web 网站程序集；`.exe` 是启动壳，真正逻辑在 `.dll` |
| `SpiritDesk.Shell.dll` / `SpiritDesk.Shell.exe` | 桌面 WPF 程序 |
| `*.pdb` | 调试符号（断点、堆栈行号），答辩可说「调试用的，发布可不带」 |

#### ② 依赖清单与运行时配置

| 文件 | 含义 |
|------|------|
| `*.deps.json` | 列出这个项目依赖哪些 NuGet 包、其它项目 DLL |
| `*.runtimeconfig.json` | 告诉运行时用哪个 .NET 版本、是否启用 ASP.NET Core 等 |

#### ③ 从 NuGet / 被引用项目「拷贝过来」的 DLL

Web 和 Shell 的 `bin` 里会有大量 `Microsoft.*.dll`、`Microsoft.EntityFrameworkCore.dll`、`Microsoft.Data.Sqlite.dll` 等——因为 `.csproj` 里引用了包，build 时会把运行需要的程序集**复制到输出目录**，这样换一台机器只要装了 .NET 运行时，就能直接跑，不必再装一遍 NuGet。

Shell 的 `bin` 里还会出现 **`SpiritDesk.Web.dll`、`SpiritDesk.Core.dll`**：因为 Shell 的 `.csproj` 引用了 Web 项目，编译 Shell 时会把 Web 的产出一并拷进 Shell 的输出目录，方便内嵌 WebView2 时启动同一套网站。

#### ④ 配置文件与运行期数据（Web / Shell 更明显）

| 文件/夹 | 来源 |
|---------|------|
| `appsettings.json`、`appsettings.Development.json` | 从 Web 项目根目录**复制**到 `bin`（内容配置） |
| `data/`（若有） | 运行后生成的 SQLite 等，**不是**源码里的，是程序跑起来写的 |
| `runtimes/` | 含各平台原生库（如 SQLite 的 `e_sqlite3`） |

#### ⑤ 网站静态资源（Web 专用，.NET 9 方式）

本仓库 build 后会有：

- `SpiritDesk.Web.staticwebassets.runtime.json`  
- `SpiritDesk.Web.staticwebassets.endpoints.json`  

它们描述 **`wwwroot` 里 CSS/JS/图片** 在运行时如何被映射成 URL；不一定在 `bin` 根下再拷一整份 `wwwroot` 文件夹（框架用静态 Web 资源机制）。浏览器访问 `/css/site.css` 时，宿主根据这些 json + 开发/发布模式提供文件。

**和 `wwwroot` 专篇的关系**：页面 SSR 见 [11-cshtml与wwwroot一次请求如何生成页面.md](./11-cshtml与wwwroot一次请求如何生成页面.md)；静态文件如何挂到网站上见 [08-wwwroot与静态资源详解.md](./08-wwwroot与静态资源详解.md)。

#### 三个项目 `bin` 体积对比（心里有数）

| 项目 | `bin/Debug/...` 里大概有什么 | 为什么 |
|------|------------------------------|--------|
| **Core** | 只有 `SpiritDesk.Core.dll` + deps + pdb | 纯类库，无 NuGet 包、无 exe |
| **Web** | 本项目 dll/exe + EF/Sqlite 等一大堆 DLL + appsettings + staticwebassets json | 网站 + 数据库包 |
| **Shell** | Shell exe + **整份 Web 输出** + WebView2 相关 DLL | 引用了 Web，输出目录会合并依赖 |

### 2.3 为什么 Git 不提交 `obj/`、`bin/`？

仓库根目录 `.gitignore` 里有：

```
[Bb]in/
[Oo]bj/
```

原因：

1. **体积大、变化频繁**，每次编译都变，提交会污染历史  
2. **和机器、配置有关**（Debug/Release、路径），别人拉下来也没用  
3. 任何人 clone 后执行 `dotnet restore` + `dotnet build` 会**在本机重新生成**

**答辩**：`obj`、`bin` 是本地编译产物，**源代码在 `src/` 和 `.csproj` 里**，协作靠 Git 管源码，不靠传 DLL。

### 2.4 什么时候要删 `obj` / `bin`？

| 现象 | 可尝试 |
|------|--------|
| 改了代码但运行起来像旧版 | 删对应项目的 `bin`、`obj`，再 `dotnet build` |
| NuGet 包版本乱了 | 对整个解决方案 `dotnet clean`，或手动删各项目 `obj` |
| 奇怪编译错误、生成文件损坏 | 同上，相当于「干净重编」 |

命令（在仓库根目录）：

```powershell
dotnet clean
dotnet build
```

不必害怕删 `obj`：**不会删掉你的 `.cs`、`.cshtml`**，只删生成物。

### 2.5 `obj` 里若看到 `SpiritDesk.Shell_MarkupCompile.cache` 之类

Shell 是 **WPF**，会把 `.xaml` 编译成中间代码，缓存会出现在 `obj/Debug/net9.0-windows/` 下。  
**不用打开看**，知道是 WPF 构建流程即可；答辩不必背文件名。

---

## 3. 和本仓库其它目录的关系

```
DesktopCompanion/
├── SpiritDesk.sln              ← 解决方案
├── src/
│   ├── SpiritDesk.Core/
│   │   ├── SpiritDesk.Core.csproj
│   │   ├── Entities/ ...
│   │   ├── bin/  obj/         ← 编译后出现（Git 忽略）
│   ├── SpiritDesk.Web/
│   │   ├── SpiritDesk.Web.csproj
│   │   └── ...
│   └── SpiritDesk.Shell/
│       └── ...
├── temp/TempCheckDb/
│   ├── TempCheckDb.csproj      ← 临时小工具，也有独立 obj/bin
│   └── Program.cs
└── artifacts/                  ← 发布（publish）后的部署包，不是 obj
```

| 目录 | 和 obj/bin 的区别 |
|------|-------------------|
| **`artifacts/`** | 执行 `dotnet publish` 后，**刻意导出**给服务器或演示用的成品 |
| **`temp/`** | 临时项目，不参与主 `SpiritDesk.sln` 答辩主线（可选提一句） |

---

## 4. 老师可能怎么问（标准答法）

**问：`.csproj` 是什么？**  
> 单个 C# 项目的工程文件，写明目标框架、NuGet 包、项目引用和输出类型；`dotnet build` 按它编译。我们三个项目在 `src/` 下各有一个，由 `SpiritDesk.sln` 统一管理。

**问：`obj` 文件夹干什么？要不要提交？**  
> 编译中间文件和缓存，自动生成。不提交 Git，在 `.gitignore` 里忽略了；删掉后重新 build 会再生成。

**问：和 `bin` 有什么区别？**  
> `obj` 是过程产物，`bin` 是最终可运行的 DLL/exe；`dotnet run` 主要用 `bin` 里的输出。

**问：Web 的 `.csproj` 和 Core 有什么不同？**  
> Web 用 `Microsoft.NET.Sdk.Web`，会处理 Razor 页面和 `wwwroot`；Core 是普通类库 Sdk，只产出被引用的 DLL，没有网站和页面。

---

## 5. 相关文档

| 文档 | 内容 |
|------|------|
| [03-根目录文件夹说明.md](./03-根目录文件夹说明.md) | 根目录 `bin`/`obj` 一句带过 |
| [04-src里面每个项目.md](./04-src里面每个项目.md) | 三个项目业务分工；Web 的 csproj 表格 |
| [05-重要文件速查表.md](./05-重要文件速查表.md) | 文件名速查 |
| [10-SpiritDesk.Web项目与cshtml详解.md](../10-SpiritDesk.Web项目与cshtml详解.md) | Web 工程 + 每个 cshtml |

上一篇：[11-cshtml与wwwroot一次请求如何生成页面.md](./11-cshtml与wwwroot一次请求如何生成页面.md)
