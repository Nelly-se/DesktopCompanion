// =============================================================================
// Chat.cshtml.cs — 聊天页 PageModel
// =============================================================================
// 数据结构：ChatHistoryViewModel（含按精灵过滤的 CurrentConversationMessages）
// C# 语法：
//   - [BindProperty(SupportsGet = true)]：GET ?spiritId= 也绑定到 SpiritId
//   - RedirectToPage(new { spiritId = SpiritId })：路由匿名对象传参
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

[IgnoreAntiforgeryToken]
public class ChatModel(SpiritDeskService spiritDeskService) : PageModel
{
    public ChatHistoryViewModel ChatHistory { get; private set; } = default!;

    [TempData]
    public string? NoticeMessage { get; set; }

    [BindProperty]
    public string MessageText { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string SpiritId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public int? FocusMessageId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync()) return RedirectToPage("/Spirits/Select");
        ChatHistory = await spiritDeskService.BuildChatHistoryViewModelAsync(SpiritId);
        SpiritId = ChatHistory.CurrentSpirit.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync()
    {
        await spiritDeskService.SendMessageAsync(MessageText, SpiritId);
        NoticeMessage = "新消息已发送，聊天记录已更新。";
        return RedirectToPage(new { spiritId = SpiritId });
    }

    public IActionResult OnPostSwitchSpiritAsync()
    {
        if (string.IsNullOrWhiteSpace(SpiritId))
        {
            return RedirectToPage();
        }

        return RedirectToPage(new { spiritId = SpiritId });
    }
}
