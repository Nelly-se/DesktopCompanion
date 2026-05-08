using Microsoft.EntityFrameworkCore;
using SpiritDesk.Web.Data;
using SpiritDesk.Web.Helpers;
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

        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        var databasePath = Path.Combine(dataDirectory, "spiritdesk.db");
        DatabaseBootstrapper.EnsureCompatibleDatabase(databasePath);

        builder.Services.AddRazorPages();
        builder.Services.AddDbContext<SpiritDeskDbContext>(optionsBuilder =>
            optionsBuilder.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddScoped<SpiritPersonaService>();
        builder.Services.AddScoped<SpiritDeskService>();

        var app = builder.Build();
        app.UseExceptionHandler("/Error");
        app.UseRouting();
        app.UseStaticFiles();
        app.UseAuthorization();
        app.MapRazorPages();

        return app;
    }
}
