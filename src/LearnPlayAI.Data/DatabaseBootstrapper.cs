using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data;

public static class DatabaseBootstrapper
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS video_entries (
            video_id TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            title TEXT NOT NULL,
            duration_ticks INTEGER NOT NULL DEFAULT 0,
            last_position_ticks INTEGER NOT NULL DEFAULT 0,
            last_watched_utc TEXT NULL,
            added_utc TEXT NOT NULL,
            is_missing INTEGER NOT NULL DEFAULT 0
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_video_entries_content_hash ON video_entries(content_hash);

        CREATE TABLE IF NOT EXISTS playlists (
            playlist_id TEXT PRIMARY KEY,
            name TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS playlist_items (
            playlist_id TEXT NOT NULL,
            video_id TEXT NOT NULL,
            position INTEGER NOT NULL,
            PRIMARY KEY (playlist_id, video_id)
        );

        CREATE TABLE IF NOT EXISTS app_settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS shortcut_bindings (
            action_id TEXT PRIMARY KEY,
            key_gesture TEXT NOT NULL
        );
        """;

    public static SqliteConnection OpenDatabase(string dbPath)
    {
        EnsureUsableDatabaseFile(dbPath);

        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        using (var createSchema = connection.CreateCommand())
        {
            createSchema.CommandText = Schema;
            createSchema.ExecuteNonQuery();
        }

        return connection;
    }

    private static void EnsureUsableDatabaseFile(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            var result = (string)check.ExecuteScalar()!;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new SqliteException("quick_check failed", 11);
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 11 or 26)
        {
            var backupPath = $"{dbPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak";
            File.Copy(dbPath, backupPath, overwrite: true);
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}
