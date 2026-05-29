// =============================================================================
// Logout.cshtml.cs — 登出：SignOutAsync 清除 Cookie → 跳转 Login
// =============================================================================

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Auth;

namespace SpiritDesk.Web.Pages;

/// <summary>登出 /Logout — POST 清除 Cookie 并跳转登录页（Logout.cshtml 通常只有表单）。</summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class LogoutModel(IConfiguration configuration) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        if (SpiritDeskAuthHelper.IsDemoAuthEnabled(configuration))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        }

        return RedirectToPage("/Login");
    }
}
