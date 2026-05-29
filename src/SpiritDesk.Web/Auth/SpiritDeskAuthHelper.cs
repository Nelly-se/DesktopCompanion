// =============================================================================
// SpiritDeskAuthHelper.cs — 从 IConfiguration 读取 SpiritDesk:Auth 配置
// =============================================================================
// 数据结构：无自定义类；读 appsettings.json 节 SpiritDesk:Auth
// C# 语法：
//   - static class：纯函数式配置读取
//   - out 参数：TryGetBootstrapCredentials 用 out string 返回多个值（元组替代前的常见写法）
//   - ?. 空条件：配置缺失时不抛 NullReferenceException
//   - GetValue&lt;bool&gt; / GetValue("AllowRegistration", true)：泛型与默认值重载
// =============================================================================

using Microsoft.Extensions.Configuration;

namespace SpiritDesk.Web.Auth;

/// <summary>认证开关与演示账号配置，不操作 Cookie 或数据库。</summary>
public static class SpiritDeskAuthHelper
{
    /// <summary>是否启用 Cookie 登录门禁。</summary>
    public static bool IsDemoAuthEnabled(IConfiguration configuration)
    {
        return configuration.GetSection("SpiritDesk:Auth").GetValue<bool>("Enabled");
    }

    /// <summary>从配置读取可选演示账号；成功时 out 参数带出用户名与密码。</summary>
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
