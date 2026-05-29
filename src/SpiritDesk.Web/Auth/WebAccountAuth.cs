// =============================================================================
// WebAccountAuth.cs — 用户名规范化与正则校验
// =============================================================================
// 数据结构：无；依赖编译期生成的 Regex 源生成器
// C# 语法：
//   - partial class + partial method UsernamePattern：源生成器在编译时生成 Regex 实现
//   - [GeneratedRegex(...)]：.NET 7+ 源生成正则，避免运行时编译 Regex
//   - ToLowerInvariant()：文化无关小写，作数据库唯一键
// =============================================================================

using System.Text.RegularExpressions;

namespace SpiritDesk.Web.Auth;

/// <summary>3～32 位字母/数字/下划线/连字符；与 Login/Register 共用。</summary>
public static partial class WebAccountAuth
{
    [GeneratedRegex(@"^[\p{L}\p{N}_\-]{3,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static bool IsValidUsername(string username)
    {
        var t = username.Trim();
        return UsernamePattern().IsMatch(t);
    }
}
