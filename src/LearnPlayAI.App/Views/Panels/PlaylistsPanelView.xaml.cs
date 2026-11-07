using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class PlaylistsPanelView : UserControl
{
    public PlaylistsViewModel ViewModel { get; }

    public PlaylistsPanelView(PlaylistService playlistService, LibraryService libraryService, Action<Guid, Guid> onPlay)
    {
        InitializeComponent();
        ViewModel = new PlaylistsViewModel(playlistService, libraryService, onPlay);
        DataContext = ViewModel;
    }

    private void OnPlayItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlaylistItemDisplay item } button) return;
        if (FindAncestorPlaylist(button) is not { } playlist) return;
        ViewModel.PlayCommand.Execute((playlist, item.VideoId));
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlaylistItemDisplay item } button) return;
        if (FindAncestorPlaylist(button) is not { } playlist) return;
        ViewModel.RemoveVideoCommand.Execute((playlist, item.VideoId));
    }

    private static PlaylistDisplay? FindAncestorPlaylist(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: PlaylistDisplay playlist })
            {
                return playlist;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
