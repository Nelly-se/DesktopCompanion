# SpiritDesk 后端阿里云部署方案

本项目建议将 [SpiritDesk.Web](D:/software_construction/program/src/SpiritDesk.Web) 作为云端部署对象。  
`SpiritDesk.Shell` 是本地 Windows 桌面壳，不部署到 Linux 服务器。

## 1. 推荐部署架构

课程答辩 / 单实例演示推荐：

- 阿里云 ECS Linux 服务器 1 台
- Nginx 反向代理
- `SpiritDesk.Web` 以 `systemd` 服务运行，或使用 Docker 运行
- 数据库先保留 SQLite
- 域名 + HTTPS 证书可选

若准备长期运行或多人访问，建议升级为：

- ECS 或 ACK
- Nginx / ALB
- ApsaraDB RDS for MySQL 或 PostgreSQL
- 日志接入 SLS
- 监控接入 CloudMonitor

## 2. 当前项目适合哪种方式

### 方式 A：直接发布到 ECS

适合：

- 课程项目
- 单机演示
- 成本低
- 排查简单

### 方式 B：Docker 部署到 ECS

适合：

- 团队协作交付
- 环境一致性要求更高
- 后续想扩容或迁移

本仓库已经补充：

- `src/SpiritDesk.Web/Dockerfile`
- `deploy/systemd/spiritdesk-web.service`
- `deploy/nginx/spiritdesk.conf`

## 3. 服务器建议配置

- 操作系统：Ubuntu 22.04 LTS 或 Alibaba Cloud Linux 3
- CPU：2 vCPU 起
- 内存：2 GB 起
- 磁盘：40 GB 起

安全组建议仅开放：

- `22`：SSH
- `80`：HTTP
- `443`：HTTPS

应用内部监听 `127.0.0.1:8080`，不要直接对公网暴露 Kestrel。

## 4. 方式 A：ECS + systemd + Nginx

### 4.1 安装运行时

参考微软官方文档安装 `.NET 9 Runtime` 与 `ASP.NET Core Runtime`。

### 4.2 发布项目

在本地执行：

```powershell
dotnet publish .\src\SpiritDesk.Web\SpiritDesk.Web.csproj -c Release -o .\publish\web
```

将 `publish/web` 上传到服务器，例如：

```bash
/opt/spiritdesk/web
```

### 4.3 配置环境变量

建议不要把密钥写回 `appsettings.json`，改为系统环境变量：

```bash
export OpenAI__ApiKey="your-key"
export OpenAI__Model="gpt-4.1-mini"
export OpenAI__BaseUrl="https://api.openai.com/v1"
```

如果使用 `systemd`，建议改写到单独的环境文件，再由 service 引用。

### 4.4 安装服务

复制：

```text
deploy/systemd/spiritdesk-web.service
```

到：

```text
/etc/systemd/system/spiritdesk-web.service
```

然后执行：

```bash
sudo systemctl daemon-reload
sudo systemctl enable spiritdesk-web
sudo systemctl start spiritdesk-web
sudo systemctl status spiritdesk-web
```

### 4.5 配置 Nginx

复制：

```text
deploy/nginx/spiritdesk.conf
```

到：

```text
/etc/nginx/conf.d/spiritdesk.conf
```

检查并重载：

```bash
sudo nginx -t
sudo systemctl reload nginx
```

### 4.6 验证

```bash
curl http://127.0.0.1:8080/healthz
curl http://127.0.0.1:8080/api/companion/state
curl http://your-domain-or-ip/healthz
```

## 5. 方式 B：Docker 部署

在项目根目录执行：

```bash
docker build -f src/SpiritDesk.Web/Dockerfile -t spiritdesk-web:latest .
docker run -d \
  --name spiritdesk-web \
  -p 127.0.0.1:8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e OpenAI__ApiKey="your-key" \
  -e OpenAI__Model="gpt-4.1-mini" \
  -e OpenAI__BaseUrl="https://api.openai.com/v1" \
  -v /opt/spiritdesk/data:/app/data \
  spiritdesk-web:latest
```

然后仍然由 Nginx 对外提供 80/443。

## 6. 当前后端的云部署注意事项

### 已具备

- Razor Pages 站点可独立运行
- SQLite 本地持久化
- DataProtection 密钥持久化
- 反向代理转发支持
- 健康检查接口 `/healthz`
- 任务、聊天、成长、精灵选择等核心业务闭环

### 仍建议补强

- 登录鉴权
- 接口文档 / Swagger
- 更系统的日志采集
- 数据库迁移机制
- 生产数据库从 SQLite 迁移到 RDS
- 密钥托管与配置中心

## 7. 推荐上线节奏

### 第一阶段：答辩上线

- 单台 ECS
- SQLite
- systemd 或 Docker
- Nginx
- `/healthz` 检查

### 第二阶段：正式版本

- 数据迁移到 RDS
- 接入 HTTPS
- 日志接入 SLS
- 监控接入 CloudMonitor
- 增加备份与自动部署
