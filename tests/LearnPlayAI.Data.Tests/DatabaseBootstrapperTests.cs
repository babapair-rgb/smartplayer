using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data.Tests;

public class DatabaseBootstrapperTests
{
    [Fact]
    public void OpenDatabase_NewFile_CreatesAllTables()
    {
        var path = TempDbPath();
        try
        {
            using var connection = DatabaseBootstrapper.OpenDatabase(path);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var reader = command.ExecuteReader();
            var tables = new List<string>();
            while (reader.Read()) tables.Add(reader.GetString(0));

            Assert.Contains("video_entries", tables);
            Assert.Contains("playlists", tables);
            Assert.Contains("playlist_items", tables);
            Assert.Contains("app_settings", tables);
            Assert.Contains("shortcut_bindings", tables);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void OpenDatabase_ReopeningExistingFile_Succeeds()
    {
        var path = TempDbPath();
        try
        {
            using (var first = DatabaseBootstrapper.OpenDatabase(path)) { }
            using var second = DatabaseBootstrapper.OpenDatabase(path);
            Assert.Equal(System.Data.ConnectionState.Open, second.State);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void OpenDatabase_CorruptFile_BacksUpAndCreatesFreshDatabase()
    {
        var path = TempDbPath();
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03, 0x04 }); // not a valid SQLite file
        try
        {
            using var connection = DatabaseBootstrapper.OpenDatabase(path);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM video_entries;";
            var count = (long)command.ExecuteScalar()!;
            Assert.Equal(0, count);

            var backupExists = Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*.bak").Length == 1;
            Assert.True(backupExists);
        }
        finally
        {
            SafeDelete(path);
            foreach (var backup in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*.bak"))
            {
                SafeDelete(backup);
            }
        }
    }

    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    private static void SafeDelete(string path)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
    }
}
