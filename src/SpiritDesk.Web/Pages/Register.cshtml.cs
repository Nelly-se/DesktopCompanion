using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Auth;
using SpiritDesk.Web.Data;

namespace SpiritDesk.Web.Pages;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class RegisterModel(
    IConfiguration configuration,
    SpiritDeskDbContext dbContext,
    IPasswordHasher<WebAccount> passwordHasher) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (!SpiritDeskAuthHelper.IsDemoAuthEnabled(configuration))
        {
            return RedirectToPage("/Index");
        }

        if (!SpiritDeskAuthHelper.IsRegistrationAllowed(configuration))
        {
            return RedirectToPage("/Login");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        ViewData["Title"] = "注册";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "注册";

        if (!SpiritDeskAuthHelper.IsDemoAuthEnabled(configuration))
        {
            return RedirectToPage("/Index");
        }

        if (!SpiritDeskAuthHelper.IsRegistrationAllowed(configuration))
        {
            return RedirectToPage("/Login");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        if (!WebAccountAuth.IsValidUsername(Username))
        {
            ErrorMessage = "用户名长度为 3～32，可使用字母、数字、下划线与短横线。";
            return Page();
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "密码至少 6 位。";
            return Page();
        }

        if (Password.Length > 128)
        {
            ErrorMessage = "密码过长。";
            return Page();
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "两次输入的密码不一致。";
            return Page();
        }

        var normalized = WebAccountAuth.NormalizeUsername(Username);

        if (SpiritDeskAuthHelper.TryGetBootstrapCredentials(configuration, out var bootUser, out _)
            && string.Equals(normalized, WebAccountAuth.NormalizeUsername(bootUser), StringComparison.Ordinal))
        {
            ErrorMessage = "该用户名已被系统预留（演示账号），请换一个。";
            return Page();
        }

        if (await dbContext.WebAccounts.AnyAsync(u => u.Username == normalized).ConfigureAwait(false))
        {
            ErrorMessage = "该用户名已被注册。";
            await Task.Delay(Random.Shared.Next(80, 200)).ConfigureAwait(false);
            return Page();
        }

        var displayName = Username.Trim();
        var account = new WebAccount
        {
            Username = normalized,
            CreatedAt = DateTimeOffset.UtcNow
        };
        account.PasswordHash = passwordHasher.HashPassword(account, Password);

        dbContext.WebAccounts.Add(account);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        TempData["LoginNotice"] = $"账号「{displayName}」已创建，请登录。";
        return RedirectToPage("/Login");
    }
}
