namespace LearnPlayAI.Core.Models;

public sealed class Playlist
{
    public required Guid PlaylistId { get; init; }
    public required string Name { get; set; }
    public List<PlaylistItem> Items { get; init; } = new();
}

public sealed class PlaylistItem
{
    public required Guid VideoId { get; init; }
    public required int Position { get; set; }
}
