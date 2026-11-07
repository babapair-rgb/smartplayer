using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class LibraryPanelView : UserControl
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPanelView(LibraryService libraryService, PlaylistService playlistService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new LibraryViewModel(libraryService, playlistService, onOpen);
        DataContext = ViewModel;
    }

    private void OnAddToPlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: Playlist playlist, Tag: VideoEntry video } comboBox)
        {
            ViewModel.AddToPlaylistCommand.Execute((video, playlist));
            comboBox.SelectedItem = null;
        }
    }
}
