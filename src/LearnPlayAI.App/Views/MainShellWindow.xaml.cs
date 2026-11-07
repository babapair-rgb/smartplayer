using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayerView _playerView;
    private readonly Input.KeyboardShortcutManager _shortcutManager;

    private Guid? _activePlaylistId;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlaylistService playlistService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView,
        Input.KeyboardShortcutManager shortcutManager)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;
        _playlistService = playlistService;
        _playerViewModel = playerViewModel;
        _shortcutManager = shortcutManager;

        // ThemeManager.Apply must merge the theme dictionary into Application.Resources
        // *before* InitializeComponent() parses this window's XAML: the nav buttons use
        // {StaticResource NavButtonStyle}, which resolves eagerly at parse time against
        // whatever is already in Application.Resources. Calling InitializeComponent()
        // first (as a literal reading of the task brief would do) throws a
        // XamlParseException ("Cannot find resource named 'NavButtonStyle'") because no
        // theme dictionary has been merged in yet.
        _themeManager.Apply(_settingsService.Current.Theme);

        InitializeComponent();

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        _playerViewModel.Engine.StateChanged += OnEngineStateChanged;
        _playerViewModel.Engine.ErrorOccurred += OnEngineError;

        RegisterShortcutActions();
        ReapplyShortcuts();

        CenterContent.Content = BuildHomePlaceholder();
    }

    private void RegisterShortcutActions()
    {
        _shortcutManager.RegisterAction("PlayPause", () => _playerViewModel.PlayPauseCommand.Execute(null));
        _shortcutManager.RegisterAction("Stop", () => _playerViewModel.StopCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekForward", () => _playerViewModel.SeekForwardCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekBackward", () => _playerViewModel.SeekBackwardCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeUp", () => _playerViewModel.VolumeUpCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeDown", () => _playerViewModel.VolumeDownCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleMute", () => _playerViewModel.ToggleMuteCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleFullScreen", ToggleFullScreen);
        _shortcutManager.RegisterAction("StepFrame", () => _playerViewModel.StepFrameCommand.Execute(null));
        _shortcutManager.RegisterAction("Screenshot", () => _playerViewModel.ScreenshotCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedUp", () => _playerViewModel.SpeedUpCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedDown", () => _playerViewModel.SpeedDownCommand.Execute(null));
    }

    public void ReapplyShortcuts() => _shortcutManager.Apply(this, _settingsService.Shortcuts);

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.LibraryPanelView(_libraryService, _playlistService, OpenVideo);

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.PlaylistsPanelView(_playlistService, _libraryService, PlayFromPlaylist);

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.RecentPanelView(_libraryService, OpenVideo);

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

    private UIElement BuildHomePlaceholder()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select Library, Playlists, or Recent from the left, or open a file directly:",
            Foreground = (Brush)FindResource("Text.Primary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var openButton = new Button
        {
            Content = "Open Video File...",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Click += async (_, _) => await OpenFileDialogAsync();
        panel.Children.Add(openButton);
        return panel;
    }

    private async System.Threading.Tasks.Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var entry = await _libraryService.RegisterFileAsync(dialog.FileName);
        OpenVideo(entry);
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        VideoEntry? lastRegistered = null;
        foreach (var file in files)
        {
            lastRegistered = await _libraryService.RegisterFileAsync(file);
        }
        if (lastRegistered is not null)
        {
            OpenVideo(lastRegistered);
        }
    }

    private void OpenVideo(VideoEntry entry)
    {
        _activePlaylistId = null;
        OpenVideoEntry(entry);
    }

    private async void PlayFromPlaylist(Guid playlistId, Guid startVideoId)
    {
        var entry = await _libraryService.GetByIdAsync(startVideoId);
        if (entry is null) return;
        _activePlaylistId = playlistId;
        OpenVideoEntry(entry);
    }

    private void OpenVideoEntry(VideoEntry entry)
    {
        CenterContent.Content = _playerView;
        _playerView.PlayVideo(entry);
    }

    private void OnEngineStateChanged(object? sender, PlaybackState state)
    {
        if (state != PlaybackState.Ended || _activePlaylistId is null) return;
        Dispatcher.Invoke(() => _ = AdvancePlaylistAsync());
    }

    private void OnEngineError(object? sender, string message)
    {
        if (_activePlaylistId is null) return;
        Dispatcher.Invoke(() => _ = AdvancePlaylistAsync());
    }

    private async System.Threading.Tasks.Task AdvancePlaylistAsync()
    {
        if (_activePlaylistId is null) return;

        var playlist = await _playlistService.GetPlaylistAsync(_activePlaylistId.Value);
        if (playlist is null)
        {
            _activePlaylistId = null;
            return;
        }

        var ordered = playlist.Items.OrderBy(i => i.Position).ToList();
        var currentIndex = ordered.FindIndex(i => i.VideoId == _playerViewModel.CurrentVideoId);
        if (currentIndex < 0 || currentIndex + 1 >= ordered.Count)
        {
            _activePlaylistId = null;
            return;
        }

        var nextEntry = await _libraryService.GetByIdAsync(ordered[currentIndex + 1].VideoId);
        if (nextEntry is not null)
        {
            OpenVideoEntry(nextEntry);
        }
    }

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }
}
