using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Repositories;

public interface ISettingsRepository
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync();
    Task SaveShortcutAsync(ShortcutBinding binding);
}
