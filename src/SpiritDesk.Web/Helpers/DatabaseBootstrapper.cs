using Microsoft.Data.Sqlite;

namespace SpiritDesk.Web.Helpers;

public static class DatabaseBootstrapper
{
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
                if (!TableContainsColumns(connection, tableName, requiredColumns))
                {
                    ResetDatabase(databasePath, $"{tableName} schema mismatch");
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            ResetDatabase(databasePath, $"schema validation failed: {exception.GetType().Name}");
        }
    }

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

    private static void ResetDatabase(string databasePath, string reason)
    {
        try
        {
            var directory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
            var backupPath = Path.Combine(
                directory,
                $"spiritdesk.backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");

            if (File.Exists(databasePath))
            {
                File.Move(databasePath, backupPath);
            }

            Console.WriteLine($"[SpiritDesk] Reset local database: {reason}. Backup saved to {backupPath}");
        }
        catch (IOException)
        {
            SafeDelete(databasePath);
            DeleteIfExists($"{databasePath}-shm");
            DeleteIfExists($"{databasePath}-wal");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch
        {
        }
    }
}
