using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly Action<VideoEntry> _onOpen;

    public ObservableCollection<VideoEntry> Items { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();

    public LibraryViewModel(LibraryService libraryService, PlaylistService playlistService, Action<VideoEntry> onOpen)
    {
        _libraryService = libraryService;
        _playlistService = playlistService;
        _onOpen = onOpen;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in await _libraryService.GetLibraryAsync())
        {
            Items.Add(entry);
        }

        Playlists.Clear();
        foreach (var playlist in await _playlistService.GetPlaylistsAsync())
        {
            Playlists.Add(playlist);
        }
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
        {
            await _libraryService.RegisterFileAsync(file);
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private void Open(VideoEntry entry) => _onOpen(entry);

    [RelayCommand]
    private async Task Remove(VideoEntry entry)
    {
        await _libraryService.RemoveAsync(entry.VideoId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddToPlaylist((VideoEntry Video, Playlist Playlist) args) =>
        await _playlistService.AddVideoAsync(args.Playlist.PlaylistId, args.Video.VideoId);
}
