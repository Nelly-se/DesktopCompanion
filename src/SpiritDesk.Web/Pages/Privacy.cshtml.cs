// =============================================================================
// Privacy.cshtml.cs — 隐私政策占位页（模板自带，OnGet 空实现）
// =============================================================================
// C# 语法：经典构造函数注入 ILogger&lt;PrivacyModel&gt;（非主构造函数写法）
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SpiritDesk.Web.Pages;

public class PrivacyModel : PageModel
{
    private readonly ILogger<PrivacyModel> _logger;

    public PrivacyModel(ILogger<PrivacyModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}

