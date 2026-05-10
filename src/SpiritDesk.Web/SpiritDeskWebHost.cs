using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        builder.Services.AddRazorPages();
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
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapHealthChecks("/healthz");

        app.MapGet("/api/companion/state", async (SpiritDeskService spiritDeskService) =>
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

        app.MapPost("/api/companion/select-spirit", async ([FromBody] CompanionSelectBody? body, SpiritDeskService spiritDeskService) =>
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

        return app;
    }
}
