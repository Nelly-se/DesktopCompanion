# 06 - C# 讲解：认证与登录注册

> **答辩向长文**（流程图、演示脚本、文件分工、老师问答）：见 [basic/15-登录注册功能详解.md](./basic/15-登录注册功能详解.md)。

## 1. 设计目标与边界

本项目认证为**可配置的登录门禁**，并与本地用户档案绑定：

- 配置关闭：本地开发、Shell 本地模式可直接访问全部页面
- 配置开启：未登录用户访问受保护页面会跳转 `/Login`
- 登录成功后，`SpiritDeskService` 会按 Cookie 用户名读取对应 `UserProfile`

账号来源：

1. `appsettings` / 环境变量中的引导账号（Bootstrap）
2. 数据库 `WebAccounts` 表（注册写入）

## 2. 配置项 SpiritDesk:Auth

`SpiritDeskAuthHelper.cs` 读取：

| 键 | 含义 |
|----|------|
| `SpiritDesk:Auth:Enabled` | 是否启用 Cookie 登录 |
| `SpiritDesk:Auth:Username` | 可选引导账号名 |
| `SpiritDesk:Auth:Password` | 可选引导密码 |
| `SpiritDesk:Auth:AllowRegistration` | 是否允许注册页 |

生产/公网示例见 `deploy/aliyun/spiritdesk.env.example`，通过 systemd `EnvironmentFile` 注入。

**Shell 本地自启 Web**时在 `StartWebProcess` 中设置 `SpiritDesk__Auth__Enabled=false`，覆盖 Development 配置，WebView2 免登录进主页。

**Shell 连云端**（环境变量 `SPIRITDESK_REMOTE_BASEURL`）不设置该项，需在 WebView2 内登录；`BuildAuthCookieHeaderAsync` 将 `SpiritDesk.Auth` 带给浮球 API。

## 3. LoginModel 流程

文件：`Pages/Login.cshtml.cs`

### OnGet

- 若未启用认证 → `RedirectToPage("/Index")`
- 若已登录 → `RedirectToLocal(ReturnUrl)`
- 否则显示登录页

### OnPostAsync

1. 校验用户名、密码非空
2. 尝试匹配配置引导账号 → 成功则 `SignInAsync`
3. 否则查 `WebAccounts` 表，`IPasswordHasher.VerifyHashedPassword`
4. 失败时统一提示「用户名或密码不正确」，并短延迟
5. 成功 → Cookie 登录 → 跳转 `ReturnUrl` 或首页

### SignInAsync

```csharp
var claims = new List<Claim> { new(ClaimTypes.Name, displayName) };
var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
```

## 4. RegisterModel 流程

- 仅在 `Enabled` 且 `AllowRegistration` 时可用
- 校验用户名格式（`WebAccountAuth.IsValidUsername`）
- 不能与引导账号同名
- 查重后 `HashPassword` 写入 `WebAccounts`
- 同步创建 `UserProfile`，`AccountUsername` 绑定规范化用户名，昵称默认使用注册名
- `TempData["LoginNotice"]` 提示注册成功 → 跳转登录页

## 5. Logout

`Logout.cshtml.cs`：`SignOutAsync` 后重定向 `/Login`。

## 6. WebAccountAuth 辅助

`Auth/WebAccountAuth.cs`：用户名规范化（如 Trim、统一大小写规则），供登录注册共用。

## 7. 与 EF 的关系

`WebAccount` 实体在 `SpiritDeskDbContext` 中配置唯一索引。密码仅存哈希，不存明文。

`UserProfile.AccountUsername` 与 `WebAccount.Username` 使用同一套规范化规则。任务、聊天和每日互动记录通过 `UserProfileId` 归属到当前档案，因此 `yzy_whu` 登录后看到的是自己的昵称、精灵、任务和聊天，而不是其他账号的历史档案。

## 8. 安全相关说明（课程报告可写）

- Cookie `HttpOnly` 降低 XSS 窃取风险
- 登录失败延迟降低简单撞库
- 引导密码不应提交到 Git；使用环境变量或服务器私密配置
- 未实现邮箱验证、找回密码、OAuth

## 9. 页面文件

- `Login.cshtml` / `Register.cshtml`：薄荷主题表单，主按钮 `action-button mint`
- 注册页底部链接回登录页

下一步阅读：[07-CSharp-Shell桌面宿主.md](./07-CSharp-Shell桌面宿主.md)
