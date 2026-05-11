# SpiritDesk / DesktopCompanion

基于 C# 的桌面精灵伴侣 Agent 应用。

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

## 目录说明

- `src/SpiritDesk.Core`：核心模型与常量
- `src/SpiritDesk.Web`：网页端与业务服务
- `src/SpiritDesk.Shell`：Windows 桌面壳（桌面浮球/宿主）
- `deploy/aliyun`：阿里云部署说明
- `deploy/nginx`：Nginx 反向代理配置
- `deploy/systemd`：systemd 服务配置

## 本地构建

```powershell
dotnet build SpiritDesk.sln
```

## 运行方式

### 1) 本地桌面版（推荐答辩演示）

```powershell
dotnet run --project .\src\SpiritDesk.Shell\SpiritDesk.Shell.csproj
```

### 2) 本地 Web 版

```powershell
dotnet run --project .\src\SpiritDesk.Web\SpiritDesk.Web.csproj
```

### 3) Shell 连接云端 Web

先设置环境变量：

```powershell
setx SPIRITDESK_REMOTE_BASEURL "http://116.62.19.40"
```

重新打开 PowerShell 后运行：

```powershell
dotnet run --project .\src\SpiritDesk.Shell\SpiritDesk.Shell.csproj
```

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
- 本项目当前不包含登录鉴权、多用户系统、RDS 迁移。
