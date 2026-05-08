using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages.Spirits;

public class SelectModel(SpiritDeskService spiritDeskService) : PageModel
{
    public List<SpiritDefinition> Spirits { get; private set; } = [];

    [BindProperty]
    public string SpiritId { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await spiritDeskService.NeedsSpiritSelectionAsync())
        {
            return RedirectToPage("/Index");
        }

        Spirits = await spiritDeskService.GetSpiritsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(SpiritId))
        {
            Spirits = await spiritDeskService.GetSpiritsAsync();
            return Page();
        }

        await spiritDeskService.SelectSpiritAsync(SpiritId);
        await spiritDeskService.ApplyWelcomeBackEffectAsync();
        return RedirectToPage("/Index");
    }
}
