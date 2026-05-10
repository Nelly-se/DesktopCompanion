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

    [BindProperty]
    public string Nickname { get; set; } = string.Empty;

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? NoticeMessage { get; set; }

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
        if (string.IsNullOrWhiteSpace(Nickname))
        {
            ErrorMessage = "请输入昵称。";
            Spirits = await spiritDeskService.GetSpiritsAsync();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SpiritId))
        {
            ErrorMessage = "请选择一位精灵伙伴。";
            Spirits = await spiritDeskService.GetSpiritsAsync();
            return Page();
        }

        await spiritDeskService.RenameAsync(Nickname);
        var result = await spiritDeskService.SelectSpiritAsync(SpiritId);
        if (!result.Succeeded)
        {
            ErrorMessage = result.Message;
            Spirits = await spiritDeskService.GetSpiritsAsync();
            return Page();
        }

        var displayName = Nickname.Trim();
        NoticeMessage = $"档案创建成功，{displayName} 已与 {result.SpiritName} 完成绑定。";
        return RedirectToPage("/Index");
    }
}
