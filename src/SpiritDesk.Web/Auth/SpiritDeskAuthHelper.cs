using Microsoft.Extensions.Configuration;

namespace SpiritDesk.Web.Auth;

public static class SpiritDeskAuthHelper
{
    /// <summary>是否启用 Cookie 登录门禁（与「演示账号」无关，开启后须登录或注册）。</summary>
    public static bool IsDemoAuthEnabled(IConfiguration configuration)
    {
        return configuration.GetSection("SpiritDesk:Auth").GetValue<bool>("Enabled");
    }

    /// <summary>可选：配置中的备用账号（例如演示），与数据库注册账号可同时存在。</summary>
    public static bool TryGetBootstrapCredentials(
        IConfiguration configuration,
        out string username,
        out string password)
    {
        username = configuration.GetSection("SpiritDesk:Auth")["Username"]?.Trim() ?? string.Empty;
        password = configuration.GetSection("SpiritDesk:Auth")["Password"] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(username) && password.Length > 0;
    }

    public static bool IsRegistrationAllowed(IConfiguration configuration)
    {
        if (!IsDemoAuthEnabled(configuration))
        {
            return false;
        }

        return configuration.GetSection("SpiritDesk:Auth").GetValue("AllowRegistration", true);
    }
}
