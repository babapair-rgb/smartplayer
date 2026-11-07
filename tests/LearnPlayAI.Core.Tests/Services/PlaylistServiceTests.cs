using LearnPlayAI.Core.Services;
using LearnPlayAI.Core.Tests.Fakes;

namespace LearnPlayAI.Core.Tests.Services;

public class PlaylistServiceTests
{
    [Fact]
    public async Task CreatePlaylistAsync_ThenGetPlaylistsAsync_ReturnsIt()
    {
        var service = new PlaylistService(new FakePlaylistRepository());

        var created = await service.CreatePlaylistAsync("Course A");
        var all = await service.GetPlaylistsAsync();

        Assert.Single(all);
        Assert.Equal(created.PlaylistId, all[0].PlaylistId);
    }

    [Fact]
    public async Task AddVideoAsync_ThenRemoveVideoAsync_UpdatesItems()
    {
        var service = new PlaylistService(new FakePlaylistRepository());
        var playlist = await service.CreatePlaylistAsync("Course A");
        var videoId = Guid.NewGuid();

        await service.AddVideoAsync(playlist.PlaylistId, videoId);
        var afterAdd = await service.GetPlaylistAsync(playlist.PlaylistId);
        Assert.Single(afterAdd!.Items);

        await service.RemoveVideoAsync(playlist.PlaylistId, videoId);
        var afterRemove = await service.GetPlaylistAsync(playlist.PlaylistId);
        Assert.Empty(afterRemove!.Items);
    }
}
