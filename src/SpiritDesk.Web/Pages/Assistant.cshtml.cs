using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

[IgnoreAntiforgeryToken]
public class AssistantModel(SpiritDeskService spiritDeskService) : PageModel
{
    public SpiritDeskViewModel Desk { get; private set; } = default!;

    [TempData]
    public string? NoticeMessage { get; set; }

    [BindProperty]
    public string SpiritId { get; set; } = string.Empty;

    [BindProperty]
    public string ActionType { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync())
        {
            return RedirectToPage("/Spirits/Select");
        }

        Desk = await spiritDeskService.BuildViewModelAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSelectSpiritAsync()
    {
        var result = await spiritDeskService.SelectSpiritAsync(SpiritId);
        NoticeMessage = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostInteractAsync()
    {
        var feedback = await spiritDeskService.PerformActionAsync(ActionType);
        NoticeMessage = feedback.ToNoticeMessage();
        return RedirectToPage();
    }
}
