using LearnPlayAI.Core.Hashing;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Services;

public sealed class LibraryService
{
    private readonly IVideoLibraryRepository _repository;
    private readonly IVideoHasher _hasher;

    public LibraryService(IVideoLibraryRepository repository, IVideoHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    public async Task<VideoEntry> RegisterFileAsync(string filePath)
    {
        var hash = _hasher.ComputeHash(filePath);
        var existing = await _repository.FindByHashAsync(hash);
        if (existing is not null)
        {
            existing.FilePath = filePath;
            existing.IsMissing = false;
            await _repository.UpsertAsync(existing);
            return existing;
        }

        var entry = new VideoEntry
        {
            VideoId = Guid.NewGuid(),
            FilePath = filePath,
            ContentHash = hash,
            Title = Path.GetFileNameWithoutExtension(filePath),
            AddedUtc = DateTime.UtcNow
        };
        await _repository.UpsertAsync(entry);
        return entry;
    }

    public Task<IReadOnlyList<VideoEntry>> GetRecentAsync(int limit = 20) => _repository.GetRecentAsync(limit);

    public Task<IReadOnlyList<VideoEntry>> GetLibraryAsync() => _repository.GetAllAsync();

    public Task<VideoEntry?> GetByIdAsync(Guid videoId) => _repository.FindByIdAsync(videoId);

    public Task UpdatePlaybackPositionAsync(Guid videoId, TimeSpan position) =>
        _repository.UpdatePositionAsync(videoId, position, DateTime.UtcNow);

    public Task UpdateDurationAsync(Guid videoId, TimeSpan duration) =>
        _repository.UpdateDurationAsync(videoId, duration);

    public Task MarkMissingAsync(Guid videoId) => _repository.SetMissingAsync(videoId, true);

    public Task RemoveAsync(Guid videoId) => _repository.DeleteAsync(videoId);
}
