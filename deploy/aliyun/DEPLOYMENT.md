# SpiritDesk 云端部署说明

## 1. 部署范围

云端只部署 `SpiritDesk.Web`。

- `SpiritDesk.Shell` 是本地 Windows 桌面壳，不部署到 Linux 服务器。
- 服务器负责提供 Web 页面和 API。

## 2. deploy 目录用途

- `deploy/aliyun`：阿里云部署文档与操作说明
- `deploy/nginx`：Nginx 反向代理配置模板
- `deploy/systemd`：`SpiritDesk.Web` 的 systemd 服务文件

## 3. 本地 Shell 如何连接云端

在本机设置：

```powershell
setx SPIRITDESK_REMOTE_BASEURL "http://116.62.19.40"
```

关闭并重新打开 PowerShell 后，启动 Shell：

```powershell
dotnet run --project .\src\SpiritDesk.Shell\SpiritDesk.Shell.csproj
```

## 4. 网页悬浮 vs 桌面浮球

- 浏览器中的悬浮精灵：页面内悬浮组件（Web UI）
- Windows 桌面浮球：系统级桌宠能力，由 `SpiritDesk.Shell` 提供

二者不是同一层能力，不要混淆。

## 5. 常见问题

### Q1: 为什么浏览器里没有真正桌宠？
因为浏览器只能运行网页。系统级桌面浮球必须由 WPF Shell 进程提供。

### Q2: 为什么 API 没配置也能聊天？
项目保留了规则兜底与演示脚本，未配置 API 时仍可完成答辩演示。

### Q3: 如何验证数据库持久化？
执行任务、签到、聊天后关闭程序，再次启动确认数据仍存在（SQLite 本地文件持久化）。

### Q4: 如何重新构建和运行？

```powershell
dotnet build SpiritDesk.sln
dotnet run --project .\src\SpiritDesk.Web\SpiritDesk.Web.csproj
dotnet run --project .\src\SpiritDesk.Shell\SpiritDesk.Shell.csproj
```
