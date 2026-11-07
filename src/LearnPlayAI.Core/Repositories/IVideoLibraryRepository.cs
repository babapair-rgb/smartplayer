using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Repositories;

public interface IVideoLibraryRepository
{
    Task<VideoEntry?> FindByIdAsync(Guid videoId);
    Task<VideoEntry?> FindByHashAsync(string contentHash);
    Task UpsertAsync(VideoEntry entry);
    Task<IReadOnlyList<VideoEntry>> GetRecentAsync(int limit);
    Task<IReadOnlyList<VideoEntry>> GetAllAsync();
    Task UpdatePositionAsync(Guid videoId, TimeSpan position, DateTime watchedUtc);
    Task UpdateDurationAsync(Guid videoId, TimeSpan duration);
    Task SetMissingAsync(Guid videoId, bool isMissing);
    Task DeleteAsync(Guid videoId);
}
