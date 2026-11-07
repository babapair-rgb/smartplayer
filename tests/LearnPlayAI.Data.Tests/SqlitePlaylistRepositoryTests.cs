using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data.Tests;

public class SqlitePlaylistRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task CreateAsync_PersistsPlaylist()
    {
        var repo = new SqlitePlaylistRepository(_dbPath);
        var playlist = await repo.CreateAsync("My Course Notes");

        var all = await repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("My Course Notes", all[0].Name);
        Assert.Equal(playlist.PlaylistId, all[0].PlaylistId);
    }

    [Fact]
    public async Task RenameAsync_UpdatesName()
    {
        var repo = new SqlitePlaylistRepository(_dbPath);
        var playlist = await repo.CreateAsync("Old Name");

        await repo.RenameAsync(playlist.PlaylistId, "New Name");

        var found = await repo.GetByIdAsync(playlist.PlaylistId);
        Assert.Equal("New Name", found!.Name);
    }

    [Fact]
    public async Task AddItemAsync_AssignsIncreasingPositions()
    {
        var repo = new SqlitePlaylistRepository(_dbPath);
        var playlist = await repo.CreateAsync("Queue");
        var videoA = Guid.NewGuid();
        var videoB = Guid.NewGuid();

        await repo.AddItemAsync(playlist.PlaylistId, videoA);
        await repo.AddItemAsync(playlist.PlaylistId, videoB);

        var found = await repo.GetByIdAsync(playlist.PlaylistId);
        var items = found!.Items.OrderBy(i => i.Position).ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(videoA, items[0].VideoId);
        Assert.Equal(videoB, items[1].VideoId);
        Assert.True(items[0].Position < items[1].Position);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesOnlyThatItem()
    {
        var repo = new SqlitePlaylistRepository(_dbPath);
        var playlist = await repo.CreateAsync("Queue");
        var videoA = Guid.NewGuid();
        var videoB = Guid.NewGuid();
        await repo.AddItemAsync(playlist.PlaylistId, videoA);
        await repo.AddItemAsync(playlist.PlaylistId, videoB);

        await repo.RemoveItemAsync(playlist.PlaylistId, videoA);

        var found = await repo.GetByIdAsync(playlist.PlaylistId);
        Assert.Single(found!.Items);
        Assert.Equal(videoB, found.Items[0].VideoId);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPlaylistAndItems()
    {
        var repo = new SqlitePlaylistRepository(_dbPath);
        var playlist = await repo.CreateAsync("Queue");
        await repo.AddItemAsync(playlist.PlaylistId, Guid.NewGuid());

        await repo.DeleteAsync(playlist.PlaylistId);

        Assert.Null(await repo.GetByIdAsync(playlist.PlaylistId));
    }

    public void Dispose()
    {
        // Release pooled SQLite handles so the file can be deleted on Windows.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
