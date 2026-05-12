using System.Text.RegularExpressions;

namespace SpiritDesk.Web.Auth;

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
