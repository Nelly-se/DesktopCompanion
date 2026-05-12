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

public static class SpiritDeskWebHost
{
    public static WebApplication Build(string[] args, string? contentRoot = null, string? webRoot = null, string[]? urls = null)
    {
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(SpiritDeskWebHost).Assembly.FullName,
            ContentRootPath = contentRoot ?? Directory.GetCurrentDirectory(),
            WebRootPath = webRoot
        };

        var builder = WebApplication.CreateBuilder(options);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();

        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }

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

        var authEnabled = SpiritDeskAuthHelper.IsDemoAuthEnabled(builder.Configuration);

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

        builder.Services.AddAuthorization();

        builder.Services.AddSingleton<IPasswordHasher<WebAccount>, PasswordHasher<WebAccount>>();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<SpiritDeskDbContext>("sqlite");
        builder.Services
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory))
            .SetApplicationName("SpiritDesk");

        var connectionString = builder.Configuration.GetConnectionString("SpiritDesk");
        builder.Services.AddDbContext<SpiritDeskDbContext>(optionsBuilder =>
            optionsBuilder.UseSqlite(string.IsNullOrWhiteSpace(connectionString)
                ? $"Data Source={databasePath}"
                : connectionString));

        builder.Services.AddHttpClient<LlmReplyService>();
        builder.Services.AddScoped<SpiritPersonaService>();
        builder.Services.AddScoped<SpiritDeskService>();

        var app = builder.Build();
        app.UseExceptionHandler("/Error");
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });
        app.UseRouting();
        app.UseStaticFiles();
        if (authEnabled)
        {
            app.UseAuthentication();
        }

        app.UseAuthorization();
        app.MapRazorPages();
        app.MapHealthChecks("/healthz").AllowAnonymous();

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

        return app;
    }
}
