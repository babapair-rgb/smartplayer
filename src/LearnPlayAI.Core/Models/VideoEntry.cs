namespace LearnPlayAI.Core.Models;

public sealed class VideoEntry
{
    public required Guid VideoId { get; init; }
    public required string FilePath { get; set; }
    public required string ContentHash { get; init; }
    public required string Title { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan LastPosition { get; set; }
    public DateTime? LastWatchedUtc { get; set; }
    public required DateTime AddedUtc { get; init; }
    public bool IsMissing { get; set; }
}
