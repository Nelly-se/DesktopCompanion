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

### Q5: 本地如何接入豆包 / 方舟？

在仓库根目录复制一份本地配置文件：

```powershell
Copy-Item .\.env.example .\.env
```

然后填入真实密钥：

```dotenv
ARK_API_KEY=你的方舟密钥
ARK_API_BASE=https://ark.cn-beijing.volces.com/api/v3
ARK_MODEL=doubao-seed-2-0-lite-260215
```

`SpiritDesk.Web` 与 `SpiritDesk.Shell` 启动的本地 Web 子进程都会自动向上查找并加载这个 `.env`。

## 6. 云端演示登录（可选）

当站点暴露在公网时，可在 **不配数据库多用户** 的前提下启用「单账号 Cookie 登录」，用于答辩演示防扫。

推荐用 **systemd 环境文件**（不把密码写进仓库）：

1. 在服务器创建目录并复制示例（示例文件：`deploy/aliyun/spiritdesk.env.example`）：

```bash
sudo mkdir -p /etc/spiritdesk
sudo nano /etc/spiritdesk/spiritdesk.env
```

2. 写入（键名勿改，值为你的账号密码）：

```bash
SpiritDesk__Auth__Enabled=true
SpiritDesk__Auth__Username=你的用户名
SpiritDesk__Auth__Password=你的强密码
ARK_API_KEY=你的方舟密钥
ARK_API_BASE=https://ark.cn-beijing.volces.com/api/v3
ARK_MODEL=doubao-seed-2-0-lite-260215
```

3. 权限与重启：

```bash
sudo chmod 600 /etc/spiritdesk/spiritdesk.env
sudo systemctl daemon-reload
sudo systemctl restart spiritdesk-web
```

`deploy/systemd/spiritdesk-web.service` 已包含 `EnvironmentFile=-/etc/spiritdesk/spiritdesk.env`。

若未配置 `Enabled=true` 或未同时填写用户名和密码，应用 **不要求登录**。

- 登录页：`/Login`
- 退出：精灵设置页底部「退出登录」

## 7. 本地默认登录（Development）

通过 **桌面壳** 或 **`dotnet run SpiritDesk.Web`** 且环境为 **Development** 时：

- Shell 启动的子进程 Web 已强制 `ASPNETCORE_ENVIRONMENT=Development`，会合并 `appsettings.Development.json`。
- 默认演示账号：**用户名 `spiritdesk`，密码 `spiritdesk`**（仅用于本机答辩演示，请勿用于公网）。

生产环境（`ASPNETCORE_ENVIRONMENT=Production`）仍以 `appsettings.json` 为准，默认 **不启用** 登录，需在服务器用 §6 的环境变量开启。

## 8. 发布到服务器（需你在本机/跳板机执行）

我无法替你 SSH 登录阿里云；请在本机构建发布后上传。

**Windows PowerShell（仓库根目录）：**

```powershell
.\deploy\scripts\publish-web.ps1
```

将输出目录 `artifacts\spiritdesk-web-publish` 内的全部文件上传到服务器 Web 目录 `/opt/spiritdesk/web/`。

如果你使用本仓库提供的 `deploy/systemd/spiritdesk-web.service`，就按下面这组路径，不要改成别的默认目录：

```powershell
scp -r .\artifacts\spiritdesk-web-publish\* user@116.62.19.40:/opt/spiritdesk/web/
```

SSH 登录服务器后：

```bash
sudo systemctl restart spiritdesk-web
sudo systemctl status spiritdesk-web --no-pager
```

确保已配置 §6 的 `/etc/spiritdesk/spiritdesk.env`，否则公网站点仍为匿名访问。
