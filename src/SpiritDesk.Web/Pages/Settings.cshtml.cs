using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

public class SettingsModel(SpiritDeskService spiritDeskService) : PageModel
{
    public SettingsViewModel Settings { get; private set; } = default!;

    [TempData]
    public string? NoticeMessage { get; set; }

    [BindProperty]
    public string Nickname { get; set; } = string.Empty;

    [BindProperty]
    public string SpiritId { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync())
        {
            return RedirectToPage("/Spirits/Select");
        }

        Settings = await spiritDeskService.BuildSettingsViewModelAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync()
    {
        if (string.IsNullOrWhiteSpace(Nickname))
        {
            NoticeMessage = "请输入昵称后再保存。";
            return RedirectToPage();
        }

        await spiritDeskService.RenameAsync(Nickname);
        NoticeMessage = $"昵称已更新为 {Nickname.Trim()}。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSelectSpiritAsync()
    {
        var result = await spiritDeskService.SelectSpiritAsync(SpiritId);
        NoticeMessage = result.Message;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetDemoDataAsync()
    {
        var feedback = await spiritDeskService.ResetDemoDataAsync();
        NoticeMessage = feedback.ToNoticeMessage();
        return RedirectToPage();
    }
}
