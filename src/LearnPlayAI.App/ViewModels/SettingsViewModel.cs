using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.App.Services;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly Action _onShortcutsChanged;

    [ObservableProperty] private ThemeMode theme;
    [ObservableProperty] private double defaultVolume;

    public ObservableCollection<ShortcutBinding> Shortcuts { get; } = new();

    public SettingsViewModel(SettingsService settingsService, ThemeManager themeManager, Action onShortcutsChanged)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
        _onShortcutsChanged = onShortcutsChanged;

        theme = settingsService.Current.Theme;
        defaultVolume = settingsService.Current.DefaultVolume;
        foreach (var binding in settingsService.Shortcuts)
        {
            Shortcuts.Add(binding);
        }
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        Theme = Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await _settingsService.SetThemeAsync(Theme);
        _themeManager.Apply(Theme);
    }

    public async Task RebindAsync(ShortcutBinding binding, string newGesture)
    {
        await _settingsService.SetShortcutAsync(binding.ActionId, newGesture);
        binding.KeyGesture = newGesture;
        _onShortcutsChanged();
    }

    partial void OnDefaultVolumeChanged(double value) => _ = _settingsService.SetDefaultVolumeAsync(value);
}
