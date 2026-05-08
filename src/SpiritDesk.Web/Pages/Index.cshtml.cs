using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

public class IndexModel(SpiritDeskService spiritDeskService) : PageModel
{
    public SpiritDeskViewModel Desk { get; private set; } = default!;

    [BindProperty]
    public string MessageText { get; set; } = string.Empty;

    [BindProperty]
    public string SpiritId { get; set; } = string.Empty;

    [BindProperty]
    public string NewTaskTitle { get; set; } = string.Empty;

    [BindProperty]
    public string NewTaskDescription { get; set; } = string.Empty;

    [BindProperty]
    public DateTime? NewTaskDueAt { get; set; }

    [BindProperty]
    public int TaskId { get; set; }

    [BindProperty]
    public string ActionType { get; set; } = string.Empty;

    [BindProperty]
    public string GameChoice { get; set; } = string.Empty;

    [BindProperty]
    public string Nickname { get; set; } = string.Empty;

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
        await spiritDeskService.SelectSpiritAsync(SpiritId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendMessageAsync()
    {
        await spiritDeskService.SendMessageAsync(MessageText);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddTaskAsync()
    {
        await spiritDeskService.AddTaskAsync(NewTaskTitle, NewTaskDescription, NewTaskDueAt);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCompleteTaskAsync()
    {
        await spiritDeskService.CompleteTaskAsync(TaskId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostInteractAsync()
    {
        await spiritDeskService.PerformActionAsync(ActionType);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPlayGameAsync()
    {
        await spiritDeskService.PlayGameAsync(GameChoice);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRenameAsync()
    {
        await spiritDeskService.RenameAsync(Nickname);
        return RedirectToPage();
    }
}
