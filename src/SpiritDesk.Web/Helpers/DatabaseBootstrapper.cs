// =============================================================================
// DatabaseBootstrapper.cs — 启动前 SQLite 表结构校验（手写 SQL，非 EF 迁移）
// =============================================================================
// 数据结构：
//   - IReadOnlyDictionary&lt;string, string[]&gt;：表名 → 必需列名数组
//   - HashSet&lt;string&gt;：PRAGMA table_info 读出的列名集合，O(1) Contains
// C# 语法：
//   - collection expression [ "Id", ... ]：C# 12 集合初始化
//   - """ ... """：原始字符串字面量，多行 SQL
//   - (tableName, requiredColumns)：foreach 解构 KeyValuePair
// =============================================================================

using Microsoft.Data.Sqlite;

namespace SpiritDesk.Web.Helpers;

/// <summary>
/// SQLite 数据库启动校验工具。
/// 作用：应用启动时检查本地 spiritdesk.db 的表结构是否仍然匹配当前实体类。
/// 如果旧数据库缺表或缺字段，就先备份旧库，再让后续 EF Core 初始化流程创建新的数据库。
/// 注意：这里不是正式的 EF Migration，而是课程项目里用于避免本地旧库阻塞启动的轻量兼容处理。
/// </summary>
public static class DatabaseBootstrapper
{
    /// <summary>表名 → 该表必须存在的列（与 EF 实体字段一致）。</summary>
    private static readonly IReadOnlyDictionary<string, string[]> RequiredSchema =
        new Dictionary<string, string[]>
        {
            ["UserProfiles"] =
            [
                "Id",
                "Nickname",
                "CurrentSpiritId",
                "Mood",
                "Affinity",
                "Level",
                "Coins",
                "LastSpiritSwitchAt",
                "CreatedAt",
                "UpdatedAt"
            ],
            ["Spirits"] =
            [
                "Id",
                "Name",
                "Mbti",
                "Title",
                "CoreRole",
                "ElementType",
                "Personality",
                "Description",
                "DialogueExample",
                "SpecialMechanism",
                "ImagePath",
                "AccentColor",
                "CheckInBonusMultiplier",
                "TaskAffinityBonus",
                "FeedMoodBonus",
                "GameCountBonus",
                "GameCoinBonus",
                "MoodDecayReduction",
                "WelcomeBackCompensation"
            ],
            ["Tasks"] =
            [
                "Id",
                "Title",
                "Description",
                "DueAt",
                "IsCompleted",
                "CreatedAt",
                "CompletedAt"
            ],
            ["ChatMessages"] =
            [
                "Id",
                "Sender",
                "Content",
                "CreatedAt"
            ],
            ["DailyActionLogs"] =
            [
                "Id",
                "ActionDate",
                "ActionType",
                "Count"
            ]
        };

    /// <summary>
    /// 确保指定路径上的 SQLite 数据库能被当前版本程序使用。
    /// 数据库文件不存在时直接返回，因为后续 <c>EnsureCreated</c> 会负责创建；
    /// 数据库文件存在时逐表检查必需列，并补建早期版本可能缺少的 WebAccounts 表。
    /// </summary>
    public static void EnsureCompatibleDatabase(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            foreach (var (tableName, requiredColumns) in RequiredSchema)
            {
                // 只要任意核心表结构不匹配，就说明这个旧库不适合继续复用。
                if (!TableContainsColumns(connection, tableName, requiredColumns))
                {
                    ResetDatabase(databasePath, $"{tableName} schema mismatch");
                    return;
                }
            }

            EnsureWebAccountsTable(connection);
        }
        catch (Exception exception)
        {
            ResetDatabase(databasePath, $"schema validation failed: {exception.GetType().Name}");
        }
    }

    /// <summary>
    /// 已有数据库不会由 EF EnsureCreated 补建新表，因此在校验通过后补建 WebAccounts（幂等）。
    /// </summary>
    private static void EnsureWebAccountsTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS "WebAccounts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebAccounts" PRIMARY KEY AUTOINCREMENT,
                "Username" TEXT NOT NULL,
                "PasswordHash" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_WebAccounts_Username" ON "WebAccounts" ("Username");
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 读取 SQLite 的表结构信息，判断某张表是否包含全部必需列。
    /// PRAGMA table_info 返回的第 2 列是列名，所以用 reader.GetString(1) 收集列名。
    /// </summary>
    private static bool TableContainsColumns(
        SqliteConnection connection,
        string tableName,
        IEnumerable<string> requiredColumns)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
        {
            return false;
        }

        return requiredColumns.All(columns.Contains);
    }

    /// <summary>
    /// 重置不兼容数据库：优先移动为备份文件，失败时再尝试删除。
    /// 同时清理 SQLite WAL 模式产生的 -shm / -wal 旁路文件，避免残留文件影响下次启动。
    /// </summary>
    private static void ResetDatabase(string databasePath, string reason)
    {
        try
        {
            var directory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
            var backupPath = Path.Combine(
                directory,
                $"spiritdesk.backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            SafeDelete($"{databasePath}-shm");
            SafeDelete($"{databasePath}-wal");

            if (File.Exists(databasePath))
            {
                File.Move(databasePath, backupPath);
            }

            Console.WriteLine($"[SpiritDesk] Reset local database: {reason}. Backup saved to {backupPath}");
        }
        catch (IOException)
        {
            SafeDelete(databasePath);
            SafeDelete($"{databasePath}-shm");
            SafeDelete($"{databasePath}-wal");
        }
    }

    /// <summary>如果文件存在就删除；由 SafeDelete 包一层异常保护后调用。</summary>
    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 安静删除文件。
    /// 数据库旁路文件可能被 SQLite 或 WebView 进程短暂占用，删除失败时忽略，不影响主启动流程。
    /// </summary>
    private static void SafeDelete(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
