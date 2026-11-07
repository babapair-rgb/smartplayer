using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using LearnPlayAI.Core.Tests.Fakes;

namespace LearnPlayAI.Core.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public async Task InitializeAsync_NoStoredShortcuts_SeedsDefaults()
    {
        var service = new SettingsService(new FakeSettingsRepository());

        await service.InitializeAsync();

        Assert.Equal(DefaultShortcuts.Create().Count, service.Shortcuts.Count);
        Assert.Contains(service.Shortcuts, s => s.ActionId == "PlayPause");
    }

    [Fact]
    public async Task InitializeAsync_StoredShortcuts_UsesStoredInsteadOfDefaults()
    {
        var repository = new FakeSettingsRepository();
        await repository.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "P" });
        var service = new SettingsService(repository);

        await service.InitializeAsync();

        Assert.Single(service.Shortcuts);
        Assert.Equal("P", service.Shortcuts[0].KeyGesture);
    }

    [Fact]
    public async Task SetThemeAsync_UpdatesCurrentAndPersists()
    {
        var repository = new FakeSettingsRepository();
        var service = new SettingsService(repository);
        await service.InitializeAsync();

        await service.SetThemeAsync(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, service.Current.Theme);
        Assert.Equal(ThemeMode.Light, (await repository.LoadSettingsAsync()).Theme);
    }

    [Fact]
    public async Task SetShortcutAsync_ExistingAction_UpdatesGesture()
    {
        var service = new SettingsService(new FakeSettingsRepository());
        await service.InitializeAsync();

        await service.SetShortcutAsync("PlayPause", "P");

        Assert.Equal("P", service.Shortcuts.Single(s => s.ActionId == "PlayPause").KeyGesture);
    }
}
