using System.Globalization;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly string _dbPath;

    public SqliteSettingsRepository(string dbPath) => _dbPath = dbPath;

    public async Task<AppSettings> LoadSettingsAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var settings = new AppSettings();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings;";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            switch (key)
            {
                case "Theme":
                    settings.Theme = Enum.Parse<ThemeMode>(value);
                    break;
                case "DefaultVolume":
                    settings.DefaultVolume = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        await UpsertSettingAsync(connection, "Theme", settings.Theme.ToString());
        await UpsertSettingAsync(connection, "DefaultVolume", settings.DefaultVolume.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var bindings = new List<ShortcutBinding>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT action_id, key_gesture FROM shortcut_bindings;";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bindings.Add(new ShortcutBinding { ActionId = reader.GetString(0), KeyGesture = reader.GetString(1) });
        }
        return bindings;
    }

    public async Task SaveShortcutAsync(ShortcutBinding binding)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO shortcut_bindings (action_id, key_gesture) VALUES ($action, $gesture)
            ON CONFLICT(action_id) DO UPDATE SET key_gesture = $gesture;
            """;
        command.Parameters.AddWithValue("$action", binding.ActionId);
        command.Parameters.AddWithValue("$gesture", binding.KeyGesture);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpsertSettingAsync(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = $value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }
}
