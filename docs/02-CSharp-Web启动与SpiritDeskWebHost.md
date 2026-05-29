# 02 - C# 讲解：Web 启动与 SpiritDeskWebHost

## 1. 程序入口 Program.cs

```csharp
var app = SpiritDesk.Web.SpiritDeskWebHost.Build(args);
app.Run();
```

整个 Web 应用只有两行：构建 `WebApplication` 并运行。复杂配置集中在静态类 `SpiritDeskWebHost`，便于 **Shell 以同样方式启动 Web**（传入自定义 `contentRoot`、`urls`）。

## 2. SpiritDeskWebHost.Build 流程概览

`SpiritDeskWebHost.cs` 中 `Build` 方法大致顺序：

1. 创建 `WebApplicationBuilder`（可指定 `ContentRootPath`、`WebRootPath`、`UseUrls`）
2. 解析数据目录 → `data/spiritdesk.db`、`data/keys`（DataProtection 密钥）
3. `DatabaseBootstrapper.EnsureCompatibleDatabase`
4. 读取 `SpiritDesk:Auth:Enabled` → 决定是否启用 Cookie 登录
5. `AddRazorPages` + 授权约定（启用认证时默认 `AuthorizeFolder("/")`）
6. `AddAuthentication` / `AddCookie`（可选）
7. `AddDbContext<SpiritDeskDbContext>`（SQLite）
8. `AddScoped` 注册 `SpiritDeskService`、`SpiritPersonaService`、`LlmReplyService`
9. `Build()` → 配置中间件管道
10. 映射 Razor Pages、健康检查、三个 **Minimal API**（companion）

## 3. 依赖注入（DI）注册表

| 服务 | 生命周期 | 作用 |
|------|----------|------|
| `SpiritDeskDbContext` | Scoped（EF 默认） | 数据库访问 |
| `SpiritDeskService` | Scoped | 业务聚合 |
| `SpiritPersonaService` | Scoped | 话术生成 |
| `LlmReplyService` | HttpClient 工厂 + Scoped | 调用大模型 |
| `IPasswordHasher<WebAccount>` | Singleton | 密码哈希 |

PageModel 通过**主构造函数注入**获取服务，例如：

```csharp
public class IndexModel(SpiritDeskService spiritDeskService) : PageModel
```

这是 C# 12 / .NET 的 primary constructor 写法，等价于构造函数里赋值字段。

## 4. 中间件管道

```text
UseExceptionHandler("/Error")
→ UseForwardedHeaders        // 反代后识别 HTTPS、客户端 IP
→ UseRouting
→ UseStaticFiles             // wwwroot
→ UseAuthentication        // 仅 authEnabled 时
→ UseAuthorization
→ MapRazorPages
→ MapHealthChecks("/healthz")
→ MapGet/MapPost (companion API)
```

**静态文件**：`wwwroot` 下 CSS、JS、精灵图片不经 Razor 即可访问。

## 5. Razor Pages 授权约定

当 `SpiritDesk:Auth:Enabled == true`：

```csharp
options.Conventions.AuthorizeFolder("/");
options.Conventions.AllowAnonymousToPage("/Login");
options.Conventions.AllowAnonymousToPage("/Register");
// ...
```

即：除显式允许匿名的页面外，访问任意页面需已登录。

## 6. Cookie 认证配置要点

- `LoginPath = "/Login"`
- `Cookie.Name = "SpiritDesk.Auth"`
- `HttpOnly = true`，`SameSite = Lax`
- `ExpireTimeSpan = 12` 小时，`SlidingExpiration = true`

Shell 连接云端且站点开启认证时，浮球请求 API 需携带同名 Cookie（见 Shell 文档）。

## 7. 数据目录与配置项

| 配置键 | 说明 |
|--------|------|
| `SpiritDesk:DataDirectory` | SQLite 与 keys 目录；默认 `AppContext.BaseDirectory/data` |
| `ConnectionStrings:SpiritDesk` | 可覆盖为完整 SQLite 连接串 |
| `SpiritDesk:Auth:*` | 见 [06-CSharp-认证与登录注册.md](./06-CSharp-认证与登录注册.md) |
| `OpenAI` / 环境变量 `ARK_*` | 大模型，见 `LlmReplyService` |

## 8. DotEnvLoader（启动前加载 .env）

`SpiritDeskWebHost` 构建早期会调用 `DotEnvLoader.LoadNearest`，从当前目录向上查找 `.env`，将 `KEY=VALUE` 写入环境变量（已存在的不覆盖）。因此可把 `ARK_API_KEY` 放在仓库根目录 `.env`，避免写入 `appsettings.json`。

## 9. 为何抽离 SpiritDeskWebHost

- Shell 启动本地 Web 时需要指定 **监听 URL** 和 **工作目录**，不能简单 `dotnet run` 默认端口。
- 单元测试或工具可复用同一套 `Build` 逻辑。
- `Program.cs` 保持极简，符合「宿主 + 库」分离习惯。

下一步阅读：[03-CSharp-数据层与EF-Core.md](./03-CSharp-数据层与EF-Core.md)
