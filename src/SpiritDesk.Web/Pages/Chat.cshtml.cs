using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

public class ChatModel(SpiritDeskService spiritDeskService) : PageModel
{
    public ChatHistoryViewModel ChatHistory { get; private set; } = default!;

    [TempData]
    public string? NoticeMessage { get; set; }

    [BindProperty]
    public string MessageText { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync())
        {
            return RedirectToPage("/Spirits/Select");
        }

        ChatHistory = await spiritDeskService.BuildChatHistoryViewModelAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync()
    {
        await spiritDeskService.SendMessageAsync(MessageText);
        NoticeMessage = "新消息已发送，聊天记录已更新。";
        return RedirectToPage();
    }
}
