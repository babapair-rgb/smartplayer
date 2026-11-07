using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public sealed class PlaylistItemDisplay
{
    public required Guid VideoId { get; init; }
    public required string Title { get; init; }
}

public sealed class PlaylistDisplay
{
    public required Guid PlaylistId { get; init; }
    public required string Name { get; init; }
    public required List<PlaylistItemDisplay> Items { get; init; }
}

public partial class PlaylistsViewModel : ObservableObject
{
    private readonly PlaylistService _playlistService;
    private readonly LibraryService _libraryService;
    private readonly Action<Guid, Guid> _onPlay;

    public ObservableCollection<PlaylistDisplay> Playlists { get; } = new();

    [ObservableProperty] private string newPlaylistName = string.Empty;

    public PlaylistsViewModel(PlaylistService playlistService, LibraryService libraryService, Action<Guid, Guid> onPlay)
    {
        _playlistService = playlistService;
        _libraryService = libraryService;
        _onPlay = onPlay;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var playlists = await _playlistService.GetPlaylistsAsync();
        var library = await _libraryService.GetLibraryAsync();
        var titleById = library.ToDictionary(v => v.VideoId, v => v.Title);

        Playlists.Clear();
        foreach (var playlist in playlists)
        {
            Playlists.Add(new PlaylistDisplay
            {
                PlaylistId = playlist.PlaylistId,
                Name = playlist.Name,
                Items = playlist.Items
                    .OrderBy(i => i.Position)
                    .Select(i => new PlaylistItemDisplay
                    {
                        VideoId = i.VideoId,
                        Title = titleById.TryGetValue(i.VideoId, out var title) ? title : "(missing video)"
                    })
                    .ToList()
            });
        }
    }

    [RelayCommand]
    private async Task CreatePlaylist()
    {
        if (string.IsNullOrWhiteSpace(NewPlaylistName)) return;
        await _playlistService.CreatePlaylistAsync(NewPlaylistName);
        NewPlaylistName = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeletePlaylist(PlaylistDisplay playlist)
    {
        await _playlistService.DeletePlaylistAsync(playlist.PlaylistId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveVideo((PlaylistDisplay Playlist, Guid VideoId) args)
    {
        await _playlistService.RemoveVideoAsync(args.Playlist.PlaylistId, args.VideoId);
        await RefreshAsync();
    }

    [RelayCommand]
    private void Play((PlaylistDisplay Playlist, Guid VideoId) args) => _onPlay(args.Playlist.PlaylistId, args.VideoId);
}
