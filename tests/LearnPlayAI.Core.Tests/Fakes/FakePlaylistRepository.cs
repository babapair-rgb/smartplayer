using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class FakePlaylistRepository : IPlaylistRepository
{
    private readonly Dictionary<Guid, Playlist> _playlists = new();

    public Task<Playlist> CreateAsync(string name)
    {
        var playlist = new Playlist { PlaylistId = Guid.NewGuid(), Name = name };
        _playlists[playlist.PlaylistId] = playlist;
        return Task.FromResult(playlist);
    }

    public Task RenameAsync(Guid playlistId, string newName)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist)) playlist.Name = newName;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid playlistId)
    {
        _playlists.Remove(playlistId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Playlist>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Playlist>>(_playlists.Values.ToList());

    public Task<Playlist?> GetByIdAsync(Guid playlistId) =>
        Task.FromResult(_playlists.TryGetValue(playlistId, out var playlist) ? playlist : null);

    public Task AddItemAsync(Guid playlistId, Guid videoId)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist))
        {
            var nextPosition = playlist.Items.Count == 0 ? 0 : playlist.Items.Max(i => i.Position) + 1;
            playlist.Items.Add(new PlaylistItem { VideoId = videoId, Position = nextPosition });
        }
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(Guid playlistId, Guid videoId)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist))
        {
            playlist.Items.RemoveAll(i => i.VideoId == videoId);
        }
        return Task.CompletedTask;
    }
}
