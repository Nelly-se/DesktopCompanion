using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel(SpiritDeskService spiritDeskService) : PageModel
{
    public SpiritDeskViewModel Desk { get; private set; } = default!;

    [TempData]
    public string? NoticeMessage { get; set; }

    [BindProperty] public string MessageText { get; set; } = string.Empty;
    [BindProperty] public string SpiritId { get; set; } = string.Empty;
    [BindProperty] public string NewTaskTitle { get; set; } = string.Empty;
    [BindProperty] public string NewTaskDescription { get; set; } = string.Empty;
    [BindProperty] public DateTime? NewTaskDueAt { get; set; }
    [BindProperty] public int TaskId { get; set; }
    [BindProperty] public string EditTaskTitle { get; set; } = string.Empty;
    [BindProperty] public string EditTaskDescription { get; set; } = string.Empty;
    [BindProperty] public DateTime? EditTaskDueAt { get; set; }
    [BindProperty] public string ActionType { get; set; } = string.Empty;
    [BindProperty] public string GameChoice { get; set; } = string.Empty;
    [BindProperty] public string Nickname { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (await spiritDeskService.NeedsSpiritSelectionAsync()) return RedirectToPage("/Spirits/Select");
        Desk = await spiritDeskService.BuildViewModelAsync();
        if (string.IsNullOrWhiteSpace(NoticeMessage) && !string.IsNullOrWhiteSpace(Desk.ReturnNotice)) NoticeMessage = Desk.ReturnNotice;
        return Page();
    }

    public async Task<IActionResult> OnPostSelectSpiritAsync() { var result = await spiritDeskService.SelectSpiritAsync(SpiritId); NoticeMessage = result.Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostSendMessageAsync() { await spiritDeskService.SendMessageAsync(MessageText); NoticeMessage = "消息已发送。"; return RedirectToPage(); }
    public async Task<IActionResult> OnPostAddTaskAsync() { NoticeMessage = (await spiritDeskService.AddTaskAsync(NewTaskTitle, NewTaskDescription, NewTaskDueAt)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostUpdateTaskAsync() { NoticeMessage = (await spiritDeskService.UpdateTaskAsync(TaskId, EditTaskTitle, EditTaskDescription, EditTaskDueAt)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostDeleteTaskAsync() { NoticeMessage = (await spiritDeskService.DeleteTaskAsync(TaskId)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostCompleteTaskAsync() { NoticeMessage = (await spiritDeskService.CompleteTaskAsync(TaskId)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostInteractAsync() { NoticeMessage = (await spiritDeskService.PerformActionAsync(ActionType)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostPlayGameAsync() { NoticeMessage = (await spiritDeskService.PlayGameAsync(GameChoice)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostRenameAsync() { await spiritDeskService.RenameAsync(Nickname); NoticeMessage = "昵称已更新。"; return RedirectToPage(); }
}
