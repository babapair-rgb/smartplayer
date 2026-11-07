using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Repositories;

public interface IPlaylistRepository
{
    Task<Playlist> CreateAsync(string name);
    Task RenameAsync(Guid playlistId, string newName);
    Task DeleteAsync(Guid playlistId);
    Task<IReadOnlyList<Playlist>> GetAllAsync();
    Task<Playlist?> GetByIdAsync(Guid playlistId);
    Task AddItemAsync(Guid playlistId, Guid videoId);
    Task RemoveItemAsync(Guid playlistId, Guid videoId);
}
