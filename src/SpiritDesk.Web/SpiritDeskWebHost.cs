// =============================================================================

// SpiritDeskWebHost.cs — ASP.NET Core Web 宿主「组装工厂」（本项目的网站大脑）

// =============================================================================

//

// 【这个文件是干什么的】

//   把 SpiritDesk 网站运行所需的全部配置集中在一个静态类里，对外只暴露

//   Build(...) → 返回 WebApplication。真正的 HTTP 服务仍由 Program.cs 调用

//   Build 后再 app.Run() 启动 Kestrel。

//

// 【谁调用它】

//   - src/SpiritDesk.Web/Program.cs（常规 dotnet run / 发布后的 Web 进程）

//   - 文档与 Shell 设计：也可传入 contentRoot / webRoot / urls，在 WPF 进程内

//     内嵌启动同一套站点（当前 Shell 本地模式多用子进程 dotnet run Web，最终仍走 Program）

//

// 【Build 方法分两大阶段】

//   1) builder 阶段（CreateBuilder 之后、Build 之前）

//      → 注册「服务」：DI 容器里放 DbContext、业务 Service、认证、Razor 约定等

//   2) app 阶段（builder.Build() 之后）

//      → 注册「中间件 + 终结点」：请求管道顺序 + MapRazorPages / MapGet 等路由

//

// 【与主业务的关系】

//   - 页面（Index / Login / Chat…）→ Razor Pages，逻辑在 *.cshtml.cs + SpiritDeskService

//   - 桌面浮球 → Minimal API /api/companion/*，由 Shell 的 CompanionBubbleWindow 轮询

//   - 数据 → SQLite 文件 spiritdesk.db，路径见下方「数据目录」段

//   - 登录 → 配置 SpiritDesk:Auth:Enabled=true 时启用 Cookie 门禁（SpiritDesk.Auth）

//

// 【相关文档】

//   docs/02-CSharp-Web启动与SpiritDeskWebHost.md

//   docs/basic/middleware/02-本项目中间件管道详解.md

// =============================================================================



using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.DataProtection;

using Microsoft.AspNetCore.HttpOverrides;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using SpiritDesk.Core.Entities;

using SpiritDesk.Web.Auth;

using SpiritDesk.Web.Data;

using SpiritDesk.Web.Helpers;

using SpiritDesk.Web.Models;

using SpiritDesk.Web.Services;



namespace SpiritDesk.Web;



/// <summary>

/// Web 宿主组装入口。不负责 Run()，只负责把 DI、中间件、Razor、API 配好并返回 <see cref="WebApplication"/>。

/// </summary>

public static class SpiritDeskWebHost

{

    /// <summary>

    /// 构建并配置完整的 SpiritDesk Web 应用（未启动监听，调用方需 <c>app.Run()</c>）。

    /// </summary>

    /// <param name="args">命令行参数，传给 <see cref="WebApplicationOptions.Args"/>。</param>

    /// <param name="contentRoot">内容根目录：读 appsettings.json、.env、计算 data 目录；默认当前工作目录。</param>

    /// <param name="webRoot">静态资源根，一般为项目下的 wwwroot；null 时使用框架默认。</param>

    /// <param name="urls">Kestrel 监听地址，例如 <c>http://127.0.0.1:5188</c>；空则走 launchSettings / 环境变量。</param>

    /// <returns>已映射路由与中间件的 <see cref="WebApplication"/>，可直接 Run。</returns>

    public static WebApplication Build(string[] args, string? contentRoot = null, string? webRoot = null, string[]? urls = null)

    {

        // ---------------------------------------------------------------------

        // 一、创建 WebApplicationBuilder（宿主骨架）

        // ---------------------------------------------------------------------

        var resolvedContentRoot = contentRoot ?? Directory.GetCurrentDirectory();



        // 从 contentRoot 向上查找 .env，把 KEY=VALUE 写入环境变量（不覆盖已有环境变量）。

        // 用途：本地放 ARK_API_KEY 等密钥，避免写进 appsettings.json。

        DotEnvLoader.LoadNearest(resolvedContentRoot);



        var options = new WebApplicationOptions

        {

            Args = args,

            ApplicationName = typeof(SpiritDeskWebHost).Assembly.FullName,

            ContentRootPath = resolvedContentRoot,

            WebRootPath = webRoot

        };



        var builder = WebApplication.CreateBuilder(options);



        // 日志：控制台 + 调试输出，便于 dotnet run 与 Visual Studio 排错。

        builder.Logging.ClearProviders();

        builder.Logging.AddConsole();

        builder.Logging.AddDebug();



        // 可选：显式指定监听 URL（Shell 内嵌或脚本启动时常用 127.0.0.1:5188）。

        if (urls is { Length: > 0 })

        {

            builder.WebHost.UseUrls(urls);

        }



        // ---------------------------------------------------------------------

        // 二、数据目录与 SQLite 路径（与 EF、DataProtection 共用）

        // ---------------------------------------------------------------------

        // 默认：{进程输出目录}/data/spiritdesk.db

        // 可在 appsettings.json 用 SpiritDesk:DataDirectory 覆盖。

        // DatabaseBootstrapper：启动前用 PRAGMA 检查表结构，旧库不兼容则备份后由 EF 重建。

        var dataDirectory = builder.Configuration["SpiritDesk:DataDirectory"];

        if (string.IsNullOrWhiteSpace(dataDirectory))

        {

            dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

        }



        Directory.CreateDirectory(dataDirectory);

        var dataProtectionDirectory = Path.Combine(dataDirectory, "keys");

        Directory.CreateDirectory(dataProtectionDirectory);

        var databasePath = Path.Combine(dataDirectory, "spiritdesk.db");

        DatabaseBootstrapper.EnsureCompatibleDatabase(databasePath);



        // 登录门禁开关：SpiritDesk:Auth:Enabled（或环境变量覆盖）。

        // false：不注册 Cookie 认证，Razor 全站匿名，Companion API 也不要求登录。

        // true：除 Login/Register/Logout/Error/Privacy 外，所有 Razor 页需已登录。

        var authEnabled = SpiritDeskAuthHelper.IsDemoAuthEnabled(builder.Configuration);



        // ---------------------------------------------------------------------

        // 三、注册服务（DI — 在 builder.Build() 之前）

        // ---------------------------------------------------------------------



        // --- Razor Pages + 页面级授权约定 ---

        // AuthorizeFolder("/")：Pages 下默认都要登录（仅当 authEnabled）。

        // AllowAnonymousToPage：白名单页面无需 Cookie 即可访问。

        builder.Services.AddRazorPages(options =>

        {

            if (authEnabled)

            {

                options.Conventions.AuthorizeFolder("/");

                options.Conventions.AllowAnonymousToPage("/Login");

                options.Conventions.AllowAnonymousToPage("/Register");

                options.Conventions.AllowAnonymousToPage("/Logout");

                options.Conventions.AllowAnonymousToPage("/Error");

                options.Conventions.AllowAnonymousToPage("/Privacy");

            }

        });



        // --- Cookie 认证（仅 authEnabled 时注册）---

        // 登录成功由 Login.cshtml.cs 调用 SignInAsync 写入 SpiritDesk.Auth。

        // HttpOnly：防 XSS 脚本读 Cookie；SameSite=Lax：同站跳转带 Cookie，跨站 POST 不带。

        if (authEnabled)

        {

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

                .AddCookie(options =>

                {

                    options.LoginPath = "/Login";

                    options.LogoutPath = "/Logout";

                    options.AccessDeniedPath = "/Login";

                    options.SlidingExpiration = true;

                    options.ExpireTimeSpan = TimeSpan.FromHours(12);

                    options.Cookie.Name = "SpiritDesk.Auth";

                    options.Cookie.HttpOnly = true;

                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                    options.Cookie.SameSite = SameSiteMode.Lax;

                });

        }



        // 授权服务：与 UseAuthorization、RequireAuthorization、[Authorize] 配合。

        builder.Services.AddAuthorization();



        // 注册/登录页哈希与校验 WebAccounts 表密码（ASP.NET Core Identity 的 Hasher）。

        builder.Services.AddSingleton<IPasswordHasher<WebAccount>, PasswordHasher<WebAccount>>();

        // 业务层读取当前 HTTP 用户（Cookie 中的 Claims）。

        builder.Services.AddHttpContextAccessor();



        // 健康检查：/healthz 可探测 SQLite 能否打开（AllowAnonymous，见下方 Map）。

        builder.Services.AddHealthChecks()

            .AddDbContextCheck<SpiritDeskDbContext>("sqlite");



        // DataProtection：加密 Cookie、防伪令牌等用的密钥，持久化到 data/keys，避免重启后 Cookie 失效。

        builder.Services

            .AddDataProtection()

            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory))

            .SetApplicationName("SpiritDesk");



        // EF Core + SQLite：连接串优先 appsettings ConnectionStrings:SpiritDesk，否则用 databasePath。

        var connectionString = builder.Configuration.GetConnectionString("SpiritDesk");

        builder.Services.AddDbContext<SpiritDeskDbContext>(optionsBuilder =>

            optionsBuilder.UseSqlite(string.IsNullOrWhiteSpace(connectionString)

                ? $"Data Source={databasePath}"

                : connectionString));



        // 业务服务：Scoped = 每个 HTTP 请求一个实例，可安全注入 DbContext。

        builder.Services.AddHttpClient<LlmReplyService>();

        builder.Services.AddScoped<SpiritPersonaService>();

        builder.Services.AddScoped<SpiritDeskService>();



        // ---------------------------------------------------------------------

        // 四、构建 app 并配置中间件管道（顺序敏感，勿随意调换）

        // ---------------------------------------------------------------------

        var app = builder.Build();



        // 未处理异常 → 重定向到 Razor 错误页 /Error（生产环境友好页）。

        app.UseExceptionHandler("/Error");



        // 反向代理场景：识别 X-Forwarded-For / X-Forwarded-Proto（部署到 Nginx 等时用）。

        app.UseForwardedHeaders(new ForwardedHeadersOptions

        {

            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto

        });



        // 路由：后续 Map* 依赖 EndpointRouting。

        app.UseRouting();



        // wwwroot 静态文件：/css/site.css、/js/site.js、/assets/images/* 等。

        app.UseStaticFiles();



        // 认证中间件：解析 Cookie，填充 HttpContext.User（须在 UseAuthorization 之前）。

        if (authEnabled)

        {

            app.UseAuthentication();

        }



        // 授权中间件：执行 [Authorize]、AuthorizeFolder、RequireAuthorization。

        app.UseAuthorization();



        // 映射所有 Razor Pages（Pages/**/*.cshtml）。

        app.MapRazorPages();



        // 存活探测，无需登录。

        app.MapHealthChecks("/healthz").AllowAnonymous();



        // ---------------------------------------------------------------------

        // 五、桌面浮球 Companion API（Minimal API，返回 JSON，非 Razor）

        // ---------------------------------------------------------------------

        // 调用方：SpiritDesk.Shell/CompanionBubbleWindow.xaml.cs

        // 开启 authEnabled 时，Shell 需把 WebView2 里登录得到的 Cookie 带到请求头，否则 401。



        // GET /api/companion/state

        // 作用：浮球启动时拉取「是否需要选精灵」+ 精灵列表 + 当前精灵 Id。

        var companionState = app.MapGet("/api/companion/state", async (SpiritDeskService spiritDeskService) =>

        {

            await spiritDeskService.EnsureInitializedAsync();

            var spirits = await spiritDeskService.GetSpiritsAsync();

            var list = spirits

                .Select(static spirit => new { id = spirit.Id, name = spirit.Name, imagePath = spirit.ImagePath })

                .ToList();



            if (await spiritDeskService.NeedsSpiritSelectionAsync())

            {

                return Results.Json(new

                {

                    needsSelection = true,

                    currentSpiritId = (string?)null,

                    spirits = list

                });

            }



            var desk = await spiritDeskService.BuildViewModelAsync();

            return Results.Json(new

            {

                needsSelection = false,

                currentSpiritId = desk.Profile.CurrentSpiritId,

                spirits = list

            });

        });

        if (authEnabled)

        {

            companionState.RequireAuthorization();

        }



        // POST /api/companion/select-spirit

        // 作用：浮球里用户选定精灵后提交 { spiritId }，写入 UserProfile.CurrentSpiritId。

        var companionSelect = app.MapPost("/api/companion/select-spirit", async ([FromBody] CompanionSelectBody? body, SpiritDeskService spiritDeskService) =>

        {

            if (body is null || string.IsNullOrWhiteSpace(body.SpiritId))

            {

                return Results.BadRequest(new { message = "Missing spirit id." });

            }



            var result = await spiritDeskService.SelectSpiritAsync(body.SpiritId.Trim());

            return result.Succeeded

                ? Results.Ok(new { message = result.Message })

                : Results.BadRequest(new { message = result.Message });

        });

        if (authEnabled)

        {

            companionSelect.RequireAuthorization();

        }



        // GET /api/companion/current

        // 作用：浮球 UI 轮询当前精灵名、头像、心情/亲密度/等级/金币、一句 statusText（人设提示）。

        // 未选精灵或异常时返回 success:false 与默认「卷卷晴」占位数据，避免浮球崩溃。

        var companionCurrent = app.MapGet("/api/companion/current", async (SpiritDeskService spiritDeskService, SpiritPersonaService personaService) =>

        {

            try

            {

                await spiritDeskService.EnsureInitializedAsync();

                var spirits = await spiritDeskService.GetSpiritsAsync();

                var fallbackSpirit = spirits.FirstOrDefault(x => x.Id == SpiritDesk.Core.Constants.SpiritIds.Light) ?? spirits.FirstOrDefault();



                if (await spiritDeskService.NeedsSpiritSelectionAsync() || fallbackSpirit is null)

                {

                    return Results.Json(new

                    {

                        success = false,

                        name = "卷卷晴",

                        title = "工作学习发动机",

                        statusText = "欢迎来到 SpiritDesk，先创建你的精灵档案吧。",

                        imageUrl = "/assets/images/spirit-light.png",

                        mood = 0,

                        affinity = 0,

                        level = 1,

                        coins = 0

                    });

                }



                var desk = await spiritDeskService.BuildViewModelAsync();

                var spirit = desk.CurrentSpirit ?? fallbackSpirit;

                var statusText = personaService.BuildHelperTip(spirit);



                return Results.Json(new

                {

                    success = true,

                    name = spirit.Name,

                    title = spirit.Title,

                    statusText,

                    imageUrl = spirit.ImagePath,

                    mood = desk.Profile.Mood,

                    affinity = desk.Profile.Affinity,

                    level = desk.Profile.Level,

                    coins = desk.Profile.Coins

                });

            }

            catch

            {

                return Results.Json(new

                {

                    success = false,

                    name = "卷卷晴",

                    title = "工作学习发动机",

                    statusText = "欢迎来到 SpiritDesk，先创建你的精灵档案吧。",

                    imageUrl = "/assets/images/spirit-light.png",

                    mood = 0,

                    affinity = 0,

                    level = 1,

                    coins = 0

                });

            }

        });

        if (authEnabled)

        {

            companionCurrent.RequireAuthorization();

        }



        // 返回已配置完毕的 app；Program.cs 或宿主代码负责 app.Run() 开始监听端口。

        return app;

    }

}

