using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class SettingsPanelView : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPanelView(SettingsService settingsService, ThemeManager themeManager, Action onShortcutsChanged)
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(settingsService, themeManager, onShortcutsChanged);
        DataContext = ViewModel;
    }

    private async void OnRebindClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutBinding binding }) return;

        var window = new Window
        {
            Title = $"Press a key for \"{binding.ActionId}\"",
            Width = 360,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Content = new TextBlock
            {
                Text = "Press any key combination...",
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        string? capturedGesture = null;
        window.PreviewKeyDown += (_, keyArgs) =>
        {
            var key = keyArgs.Key == Key.System ? keyArgs.SystemKey : keyArgs.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return;

            var gesture = new KeyGesture(key, Keyboard.Modifiers);
            capturedGesture = new KeyGestureConverter().ConvertToString(gesture);
            window.Close();
        };

        window.ShowDialog();

        if (capturedGesture is not null)
        {
            await ViewModel.RebindAsync(binding, capturedGesture);
        }
    }
}
