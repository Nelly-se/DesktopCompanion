using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
public class LoginModel(
    IConfiguration configuration,
    SpiritDeskDbContext dbContext,
    IPasswordHasher<WebAccount> passwordHasher) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public bool ShowRegisterLink => SpiritDeskAuthHelper.IsRegistrationAllowed(configuration);

    public IActionResult OnGet()
    {
        if (!SpiritDeskAuthHelper.IsDemoAuthEnabled(configuration))
        {
            return RedirectToPage("/Index");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(ReturnUrl);
        }

        ViewData["Title"] = "登录";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "登录";

        if (!SpiritDeskAuthHelper.IsDemoAuthEnabled(configuration))
        {
            return RedirectToPage("/Index");
        }

        var trimmedUser = Username.Trim();
        if (string.IsNullOrEmpty(trimmedUser) || string.IsNullOrEmpty(Password))
        {
            ErrorMessage = "请输入用户名和密码。";
            return Page();
        }

        var normalized = WebAccountAuth.NormalizeUsername(trimmedUser);

        if (SpiritDeskAuthHelper.TryGetBootstrapCredentials(configuration, out var bootUser, out var bootPass)
            && string.Equals(normalized, WebAccountAuth.NormalizeUsername(bootUser), StringComparison.Ordinal)
            && string.Equals(Password, bootPass, StringComparison.Ordinal))
        {
            await SignInAsync(trimmedUser).ConfigureAwait(false);
            return RedirectToLocal(ReturnUrl);
        }

        var account = await dbContext.WebAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == normalized)
            .ConfigureAwait(false);

        if (account is null)
        {
            ErrorMessage = "用户名或密码不正确。";
            await Task.Delay(Random.Shared.Next(120, 280)).ConfigureAwait(false);
            return Page();
        }

        var verify = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            ErrorMessage = "用户名或密码不正确。";
            await Task.Delay(Random.Shared.Next(120, 280)).ConfigureAwait(false);
            return Page();
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var tracked = await dbContext.WebAccounts.FirstAsync(u => u.Id == account.Id).ConfigureAwait(false);
            tracked.PasswordHash = passwordHasher.HashPassword(tracked, Password);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        await SignInAsync(trimmedUser).ConfigureAwait(false);
        return RedirectToLocal(ReturnUrl);
    }

    private async Task SignInAsync(string displayName)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, displayName) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity)).ConfigureAwait(false);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}
