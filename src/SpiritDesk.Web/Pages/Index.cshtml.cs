// =============================================================================
// Index.cshtml.cs — 首页 PageModel（Razor Pages 后台类）
// =============================================================================
// 配对：Index.cshtml（视图）↔ IndexModel（本文件，逻辑）
// 数据结构：SpiritDeskViewModel Desk；多个 [BindProperty] 接收表单字段
// C# 语法：
//   - 主构造函数 (SpiritDeskService ...) : PageModel：DI 注入业务服务
//   - [BindProperty]：POST 时模型绑定 form/query 到属性
//   - [TempData]：跨重定向保留 NoticeMessage（存在 Session/Cookie）
//   - OnPostXxxAsync：表单 asp-page-handler="Xxx" 对应方法名（去掉 OnPost/Async）
//   - Task<IActionResult>：异步返回 Page() 或 RedirectToPage()
//   - default!：告诉编译器 Desk 在 OnGet 前一定赋值（可空分析抑制）
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using SpiritDesk.Web.Models;
using SpiritDesk.Web.Services;

namespace SpiritDesk.Web.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel(SpiritDeskService spiritDeskService) : PageModel
{
    /// <summary>GET 时由 BuildViewModelAsync 填充，cshtml 用 @Model.Desk。</summary>
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
    public async Task<IActionResult> OnPostStreamMessageAsync()
    {
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (string.IsNullOrWhiteSpace(MessageText))
        {
            await WriteStreamEventAsync("error", "请输入消息后再发送。");
            return new EmptyResult();
        }

        try
        {
            var reply = await spiritDeskService.SendMessageStreamingAsync(
                MessageText,
                chunk => WriteStreamEventAsync("delta", chunk),
                cancellationToken: HttpContext.RequestAborted);

            await WriteStreamEventAsync(reply is null ? "error" : "done", reply is null ? "发送失败，请稍后再试。" : string.Empty);
        }
        catch (OperationCanceledException)
        {
            // 用户刷新或关闭页面时中止流式响应即可。
        }

        return new EmptyResult();
    }

    public async Task<IActionResult> OnPostAddTaskAsync() { NoticeMessage = (await spiritDeskService.AddTaskAsync(NewTaskTitle, NewTaskDescription, NewTaskDueAt)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostUpdateTaskAsync() { NoticeMessage = (await spiritDeskService.UpdateTaskAsync(TaskId, EditTaskTitle, EditTaskDescription, EditTaskDueAt)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostDeleteTaskAsync() { NoticeMessage = (await spiritDeskService.DeleteTaskAsync(TaskId)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostCompleteTaskAsync() { NoticeMessage = (await spiritDeskService.CompleteTaskAsync(TaskId)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostInteractAsync() { NoticeMessage = (await spiritDeskService.PerformActionAsync(ActionType)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostPlayGameAsync() { NoticeMessage = (await spiritDeskService.PlayGameAsync(GameChoice)).ToNoticeMessage(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostRenameAsync() { await spiritDeskService.RenameAsync(Nickname); NoticeMessage = "昵称已更新。"; return RedirectToPage(); }

    private async Task WriteStreamEventAsync(string type, string content)
    {
        var json = JsonSerializer.Serialize(new { type, content });
        await Response.WriteAsync(json + "\n", HttpContext.RequestAborted);
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
    }
}
