// =============================================================================
// Select.cshtml.cs — 首次选精灵 onboarding（/Spirits/Select）
// =============================================================================
// 数据结构：List&lt;SpiritDefinition&gt; Spirits；[TempData] ErrorMessage 跨 POST 回显
// C# 语法：
//   - = []：空集合表达式（C# 12），等价 new List&lt;SpiritDefinition&gt;()
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Core.Entities;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages.Spirits;

[IgnoreAntiforgeryToken]
public class SelectModel(SpiritDeskService spiritDeskService) : PageModel
{
    public List<SpiritDefinition> Spirits { get; private set; } = [];

    [BindProperty] public string SpiritId { get; set; } = string.Empty;
    [BindProperty] public string Nickname { get; set; } = string.Empty;

    [TempData] public string? ErrorMessage { get; set; }
    [TempData] public string? NoticeMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await spiritDeskService.NeedsSpiritSelectionAsync()) return RedirectToPage("/Index");
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

        NoticeMessage = $"档案创建成功：{Nickname.Trim()} 已与 {result.SpiritName} 绑定。";
        return RedirectToPage("/Index");
    }
}
