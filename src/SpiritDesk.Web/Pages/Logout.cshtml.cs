using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Auth;

namespace SpiritDesk.Web.Pages;

[AllowAnonymous]
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
