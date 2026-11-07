using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.Services;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    public MainShellWindow(SettingsService settingsService, ThemeManager themeManager)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;

        // ThemeManager.Apply must merge the theme dictionary into Application.Resources
        // *before* InitializeComponent() parses this window's XAML: the nav buttons use
        // {StaticResource NavButtonStyle}, which resolves eagerly at parse time against
        // whatever is already in Application.Resources. Calling InitializeComponent()
        // first (as a literal reading of the task brief would do) throws a
        // XamlParseException ("Cannot find resource named 'NavButtonStyle'") because no
        // theme dictionary has been merged in yet.
        _themeManager.Apply(_settingsService.Current.Theme);

        InitializeComponent();
        CenterContent.Content = BuildPlaceholder("Select Library, Playlists, or Recent from the left to get started.");
    }

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Library panel arrives in a later task.");

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Playlists panel arrives in a later task.");

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Recent panel arrives in a later task.");

    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var themeLabel = new TextBlock
        {
            Text = $"Current theme: {_settingsService.Current.Theme}",
            Foreground = (Brush)FindResource("Text.Primary"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var toggleButton = new Button
        {
            Content = "Toggle theme",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toggleButton.Click += async (_, _) =>
        {
            var next = _settingsService.Current.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            await _settingsService.SetThemeAsync(next);
            _themeManager.Apply(next);
            themeLabel.Text = $"Current theme: {next}";
        };
        panel.Children.Add(themeLabel);
        panel.Children.Add(toggleButton);
        CenterContent.Content = panel;
    }

    private UIElement BuildPlaceholder(string text) => new TextBlock
    {
        Text = text,
        Foreground = (Brush)FindResource("Text.Primary"),
        Margin = new Thickness(24),
        TextWrapping = TextWrapping.Wrap
    };
}
