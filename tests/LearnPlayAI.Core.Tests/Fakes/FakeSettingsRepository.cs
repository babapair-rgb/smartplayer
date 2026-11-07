using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class FakeSettingsRepository : ISettingsRepository
{
    private AppSettings _settings = new();
    private readonly Dictionary<string, ShortcutBinding> _shortcuts = new();

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(_settings);

    public Task SaveSettingsAsync(AppSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync() =>
        Task.FromResult<IReadOnlyList<ShortcutBinding>>(_shortcuts.Values.ToList());

    public Task SaveShortcutAsync(ShortcutBinding binding)
    {
        _shortcuts[binding.ActionId] = binding;
        return Task.CompletedTask;
    }
}
