using LearnPlayAI.Core.Models;
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data.Tests;

public class SqliteSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task LoadSettingsAsync_NoStoredValues_ReturnsDefaults()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        var settings = await repo.LoadSettingsAsync();

        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal(0.8, settings.DefaultVolume);
    }

    [Fact]
    public async Task SaveThenLoadSettingsAsync_RoundTrips()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveSettingsAsync(new AppSettings { Theme = ThemeMode.Light, DefaultVolume = 0.45 });

        var loaded = await repo.LoadSettingsAsync();

        Assert.Equal(ThemeMode.Light, loaded.Theme);
        Assert.Equal(0.45, loaded.DefaultVolume);
    }

    [Fact]
    public async Task SaveShortcutAsync_NewBinding_IsPersisted()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "Space" });

        var bindings = await repo.LoadShortcutsAsync();

        Assert.Single(bindings);
        Assert.Equal("Space", bindings[0].KeyGesture);
    }

    [Fact]
    public async Task SaveShortcutAsync_ExistingAction_OverwritesGesture()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "Space" });
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "P" });

        var bindings = await repo.LoadShortcutsAsync();

        Assert.Single(bindings);
        Assert.Equal("P", bindings[0].KeyGesture);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
