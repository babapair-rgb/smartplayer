using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Services;

public sealed class PlaylistService
{
    private readonly IPlaylistRepository _repository;

    public PlaylistService(IPlaylistRepository repository) => _repository = repository;

    public Task<Playlist> CreatePlaylistAsync(string name) => _repository.CreateAsync(name);
    public Task RenamePlaylistAsync(Guid id, string name) => _repository.RenameAsync(id, name);
    public Task DeletePlaylistAsync(Guid id) => _repository.DeleteAsync(id);
    public Task<IReadOnlyList<Playlist>> GetPlaylistsAsync() => _repository.GetAllAsync();
    public Task<Playlist?> GetPlaylistAsync(Guid id) => _repository.GetByIdAsync(id);
    public Task AddVideoAsync(Guid playlistId, Guid videoId) => _repository.AddItemAsync(playlistId, videoId);
    public Task RemoveVideoAsync(Guid playlistId, Guid videoId) => _repository.RemoveItemAsync(playlistId, videoId);
}
