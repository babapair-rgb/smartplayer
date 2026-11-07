using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Services;

public sealed class SettingsService
{
    private readonly ISettingsRepository _repository;

    public AppSettings Current { get; private set; } = new();
    public IReadOnlyList<ShortcutBinding> Shortcuts { get; private set; } = new List<ShortcutBinding>();

    public SettingsService(ISettingsRepository repository) => _repository = repository;

    public async Task InitializeAsync()
    {
        Current = await _repository.LoadSettingsAsync();

        var stored = await _repository.LoadShortcutsAsync();
        if (stored.Count == 0)
        {
            var defaults = DefaultShortcuts.Create();
            foreach (var binding in defaults)
            {
                await _repository.SaveShortcutAsync(binding);
            }
            Shortcuts = defaults;
        }
        else
        {
            Shortcuts = stored;
        }
    }

    public async Task SetThemeAsync(ThemeMode mode)
    {
        Current.Theme = mode;
        await _repository.SaveSettingsAsync(Current);
    }

    public async Task SetDefaultVolumeAsync(double volume)
    {
        Current.DefaultVolume = volume;
        await _repository.SaveSettingsAsync(Current);
    }

    public async Task SetShortcutAsync(string actionId, string keyGesture)
    {
        var existing = Shortcuts.FirstOrDefault(s => s.ActionId == actionId);
        if (existing is null)
        {
            existing = new ShortcutBinding { ActionId = actionId, KeyGesture = keyGesture };
            Shortcuts = Shortcuts.Append(existing).ToList();
        }
        else
        {
            existing.KeyGesture = keyGesture;
        }
        await _repository.SaveShortcutAsync(existing);
    }
}
