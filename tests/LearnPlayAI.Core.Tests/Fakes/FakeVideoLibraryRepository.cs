using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class FakeVideoLibraryRepository : IVideoLibraryRepository
{
    private readonly Dictionary<Guid, VideoEntry> _byId = new();

    public Task<VideoEntry?> FindByIdAsync(Guid videoId) =>
        Task.FromResult(_byId.TryGetValue(videoId, out var entry) ? entry : null);

    public Task<VideoEntry?> FindByHashAsync(string contentHash) =>
        Task.FromResult(_byId.Values.FirstOrDefault(e => e.ContentHash == contentHash));

    public Task UpsertAsync(VideoEntry entry)
    {
        _byId[entry.VideoId] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VideoEntry>> GetRecentAsync(int limit) =>
        Task.FromResult<IReadOnlyList<VideoEntry>>(
            _byId.Values.Where(e => e.LastWatchedUtc is not null)
                .OrderByDescending(e => e.LastWatchedUtc)
                .Take(limit)
                .ToList());

    public Task<IReadOnlyList<VideoEntry>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<VideoEntry>>(_byId.Values.ToList());

    public Task UpdatePositionAsync(Guid videoId, TimeSpan position, DateTime watchedUtc)
    {
        if (_byId.TryGetValue(videoId, out var entry))
        {
            entry.LastPosition = position;
            entry.LastWatchedUtc = watchedUtc;
        }
        return Task.CompletedTask;
    }

    public Task UpdateDurationAsync(Guid videoId, TimeSpan duration)
    {
        if (_byId.TryGetValue(videoId, out var entry))
        {
            entry.Duration = duration;
        }
        return Task.CompletedTask;
    }

    public Task SetMissingAsync(Guid videoId, bool isMissing)
    {
        if (_byId.TryGetValue(videoId, out var entry))
        {
            entry.IsMissing = isMissing;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid videoId)
    {
        _byId.Remove(videoId);
        return Task.CompletedTask;
    }
}
