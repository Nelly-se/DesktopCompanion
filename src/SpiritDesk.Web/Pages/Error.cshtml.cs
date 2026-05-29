// =============================================================================
// Error.cshtml.cs — 全局异常页 PageModel
// =============================================================================
// C# 语法：
//   - 表达式体属性 ShowRequestId => ...：根据 RequestId 是否为空决定显示
//   - Activity.Current?.Id：分布式追踪 ID，?. 空则回退 HttpContext.TraceIdentifier
// =============================================================================

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpiritDesk.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}

