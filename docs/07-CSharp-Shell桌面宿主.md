# 07 - C# 讲解：SpiritDesk.Shell 桌面宿主

## 1. 项目类型与依赖

- **SDK**：`Microsoft.NET.Sdk`，`OutputType` 为 `WinExe`
- **框架**：`net9.0-windows`，`UseWPF=true`
- **包**：`Microsoft.Web.WebView2`
- **引用**：`SpiritDesk.Core`、`SpiritDesk.Web`（保证 Web 先编译）

## 2. 应用启动 App.xaml.cs

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    var window = new MainWindow();
    window.Show();
}
```

无复杂启动逻辑，单主窗口。

## 3. MainWindow 职责

### 3.1 加载流程 OnLoaded

1. `ResolveBaseUrl(out useRemoteEndpoint)`
   - 有 `SPIRITDESK_REMOTE_BASEURL` → 远程模式
   - 否则 `GetFreePort()` 得到 `http://127.0.0.1:{port}`
2. 非远程：`StartWebProcess` 启动子进程 `dotnet SpiritDesk.Web.dll --urls {baseUrl}`
3. `WaitForServerAsync` 轮询 HTTP 直到成功
4. `Browser.EnsureCoreWebView2Async()`，设置 `Browser.Source`
5. 创建并 `Show()` `CompanionBubbleWindow`

### 3.2 关闭流程 OnClosing

- 关闭浮球
- 释放 `HttpClient`
- 若存在本地 Web 子进程则 `Kill(entireProcessTree: true)`

### 3.3 查找 Web 项目路径

`ResolveWebProjectRoot`：从 `AppContext.BaseDirectory` 向上查找 `src/SpiritDesk.Web`。

`ResolveWebEntryAssembly`：优先 `bin/Release/net9.0/SpiritDesk.Web.dll`，其次 Debug。

## 4. CompanionBubbleWindow 职责

### 4.1 双态 UI

- **收起**：`BubbleView`，圆形浮球显示精灵头像
- **展开**：`PanelView`，显示名称、状态、指标、打开主窗口/收起按钮

单击浮球切换展开；双击打开 `MainWindow`；拖拽移动窗口。

### 4.2 状态轮询

`DispatcherTimer` 每 12 秒调用 `/api/companion/current`，更新名称、文案、头像。

### 4.3 右键菜单切换精灵

菜单项为内置五精灵列表（`SpiritIds` + 显示名），不依赖 `/api/companion/state` 加载列表（避免未登录或网络失败时菜单为空）。

选择后 `POST /api/companion/select-spirit`，再刷新状态。

### 4.4 设置持久化

`ShellSettingsService` 将浮球位置、是否展开、面板尺寸、主题（mint/warm）、是否置顶写入 `%AppData%/SpiritDesk/` 下 JSON。

## 5. WebView2 与 Cookie（远程认证）

连接云端且站点开启登录时，浮球 `HttpClient` 请求 API 可能需要与浏览器共享 `SpiritDesk.Auth` Cookie。`MainWindow` 中含 `BuildAuthCookieHeaderAsync`，从 `CoreWebView2.CookieManager` 读取 Cookie 供后续请求使用（具体调用以当前代码为准）。

## 6. run-shell.ps1

```powershell
Remove-Item Env:SPIRITDESK_REMOTE_BASEURL -ErrorAction SilentlyContinue
dotnet build src/SpiritDesk.Shell/SpiritDesk.Shell.csproj -c Release
dotnet run --project ... -c Release --no-build
```

保证答辩默认走**本地一体**模式。

## 7. 与纯 Web 的差异

| 能力 | 浏览器访问 Web | Shell |
|------|----------------|-------|
| 网页内悬浮精灵 | 有（`_FloatingCompanion`） | 有（WebView 内） |
| 系统级桌面浮球 | 无 | 有（WPF 窗口） |
| 本地自动起 Web | 需手动 run Web | 可自动 |

## 8. 调试提示

- 浮球日志：`%AppData%/SpiritDesk/companion-bubble.log`
- Web 子进程输出重定向到控制台（Release 下可能不可见）
- 若 DLL 锁定，先关闭正在运行的 Shell 再编译

下一步阅读：[08-API与静态资源.md](./08-API与静态资源.md)
