using LearnPlayAI.Core.Services;
using LearnPlayAI.Core.Tests.Fakes;

namespace LearnPlayAI.Core.Tests.Services;

public class LibraryServiceTests
{
    [Fact]
    public async Task RegisterFileAsync_NewFile_CreatesEntry()
    {
        var service = new LibraryService(new FakeVideoLibraryRepository(), new StubVideoHasher());

        var entry = await service.RegisterFileAsync(@"C:\videos\lecture1.mp4");

        Assert.NotEqual(Guid.Empty, entry.VideoId);
        Assert.Equal(@"C:\videos\lecture1.mp4", entry.FilePath);
        Assert.Equal("lecture1", entry.Title);
    }

    [Fact]
    public async Task RegisterFileAsync_SameContentDifferentPath_ReusesVideoIdAndUpdatesPath()
    {
        var hasher = new StubVideoHasher(new Dictionary<string, string>
        {
            [@"C:\videos\old-location.mp4"] = "same-hash",
            [@"D:\moved\new-location.mp4"] = "same-hash"
        });
        var service = new LibraryService(new FakeVideoLibraryRepository(), hasher);

        var first = await service.RegisterFileAsync(@"C:\videos\old-location.mp4");
        var second = await service.RegisterFileAsync(@"D:\moved\new-location.mp4");

        Assert.Equal(first.VideoId, second.VideoId);
        Assert.Equal(@"D:\moved\new-location.mp4", second.FilePath);
    }

    [Fact]
    public async Task RegisterFileAsync_PreviouslyMissingFile_ClearsMissingFlag()
    {
        var repository = new FakeVideoLibraryRepository();
        var hasher = new StubVideoHasher();
        var service = new LibraryService(repository, hasher);
        var entry = await service.RegisterFileAsync(@"C:\videos\lecture1.mp4");
        await service.MarkMissingAsync(entry.VideoId);

        var reRegistered = await service.RegisterFileAsync(@"C:\videos\lecture1.mp4");

        Assert.False(reRegistered.IsMissing);
    }

    [Fact]
    public async Task UpdatePlaybackPositionAsync_PersistsPosition()
    {
        var repository = new FakeVideoLibraryRepository();
        var service = new LibraryService(repository, new StubVideoHasher());
        var entry = await service.RegisterFileAsync(@"C:\videos\lecture1.mp4");

        await service.UpdatePlaybackPositionAsync(entry.VideoId, TimeSpan.FromMinutes(3));

        var found = await service.GetByIdAsync(entry.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(3), found!.LastPosition);
    }
}
