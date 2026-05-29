// =============================================================================
// DotEnvLoader.cs — 向上查找 .env 并写入环境变量（KEY=VALUE）
// =============================================================================
// 数据结构：逐行读文件；DirectoryInfo 链表式向上 Parent 遍历目录树
// C# 语法：
//   - static class：全局工具
//   - is not null：模式匹配判断非空
//   - line[7..]：范围运算符 Range，从索引 7 切到末尾（去掉 "export "）
//   - line[..separatorIndex]：从头到 = 号前为 key
//   - 不覆盖已有环境变量：部署/容器注入优先
// =============================================================================

namespace SpiritDesk.Web.Helpers;

/// <summary>
/// .env 环境变量加载工具。
/// 作用：在 Web 宿主启动早期读取本地 .env 文件，把里面的 KEY=VALUE 写入进程环境变量。
/// 典型用途：本地开发时把豆包/方舟等 LLM API Key 放在 .env 中，代码里再通过 IConfiguration 或 Environment 读取。
/// </summary>
public static class DotEnvLoader
{
    /// <summary>
    /// 从指定目录开始向父目录逐级查找 .env 文件。
    /// 这样无论程序从项目根目录、bin 输出目录，还是 Shell 宿主目录启动，都有机会找到最近的配置文件。
    /// 找到第一个匹配文件后立即加载，不再继续向上查找。
    /// </summary>
    public static void LoadNearest(string startDirectory, string fileName = ".env")
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            return;
        }

        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            // candidate 表示当前目录下的候选 .env 路径。
            var candidate = Path.Combine(current.FullName, fileName);
            if (File.Exists(candidate))
            {
                LoadFile(candidate);
                return;
            }

            current = current.Parent;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            // 兼容 Linux/macOS 常见写法：export ARK_API_KEY=xxx。
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line[7..].Trim();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            // 不覆盖系统或部署平台已经注入的环境变量，避免本地 .env 抢优先级。
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                continue;
            }

            var value = Unquote(line[(separatorIndex + 1)..].Trim());
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>去掉首尾成对引号。</summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1]; // 索引运算符：最后一个字符
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1]; // 去掉首尾各 1 字符
            }
        }

        return value;
    }
}
