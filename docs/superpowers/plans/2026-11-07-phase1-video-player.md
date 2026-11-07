# Phase 1: Native Windows Video Player Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a fully working native Windows video player (LearnPlay AI, Phase 1) with the full application shell layout, so later AI-feature phases slot into existing panels instead of requiring a UI rewrite.

**Architecture:** .NET 8 + C# + WPF (MVVM via CommunityToolkit.Mvvm), LibVLCSharp wrapping libVLC for playback, SQLite (Microsoft.Data.Sqlite, no ORM) for local persistence. Core domain logic lives in a UI-independent class library; a separate Data library implements its repository interfaces; the WPF App project composes both via manual DI.

**Tech Stack:** .NET 8 SDK, WPF, LibVLCSharp 3.8.2 + VideoLAN.LibVLC.Windows 3.0.20, Microsoft.Data.Sqlite 8.0.8, CommunityToolkit.Mvvm 8.3.2, Microsoft.Extensions.DependencyInjection 8.0.0, xUnit.

## Global Constraints

- Platform: Windows desktop only (.NET 8, `net8.0-windows` for the App project).
- Video engine: libVLC via LibVLCSharp — must support MP4, MKV, AVI, MOV, WMV, FLV, WebM and other common formats, external and embedded subtitles, multiple audio/subtitle tracks, and high-resolution video including 4K.
- Playback speed control range: 0.25x to 4x.
- Persistence: SQLite, local file storage only — no network calls in Phase 1.
- Application layout: left sidebar (Library, Playlists, Recent Videos, Notes, Settings), center video area, right sidebar (AI Assistant, live notes), bottom transport bar — Notes and AI Assistant panels are present but inert placeholders in Phase 1.
- Dark and light themes required, switchable at runtime.
- Keyboard shortcuts must be user-configurable, not hardcoded only.
- A stable per-video identity (`VideoId`) must be established now so later phases can attach transcripts/notes/screenshots without a data-model change; identity is keyed on content hash (survives file moves/renames), not file path alone.
- No installer/packaging work in this phase (deferred to Phase 8 per the parent spec).

---

## File Structure

```
smart_vlc/
  LearnPlayAI.sln
  .gitignore
  src/
    LearnPlayAI.Core/            # domain models, repo interfaces, services, player abstraction (no UI deps)
      Models/
        VideoEntry.cs
        Playlist.cs
        AppSettings.cs
        ShortcutBinding.cs
      Hashing/
        IVideoHasher.cs
        VideoHasher.cs
      Repositories/
        IVideoLibraryRepository.cs
        IPlaylistRepository.cs
        ISettingsRepository.cs
      Services/
        LibraryService.cs
        PlaylistService.cs
        SettingsService.cs
        DefaultShortcuts.cs
      Player/
        PlaybackState.cs
        PlaybackRate.cs
        IPlayerEngine.cs
        LibVlcPlayerEngine.cs
    LearnPlayAI.Data/             # SQLite implementations of Core's repo interfaces
      DatabaseBootstrapper.cs
      SqliteVideoLibraryRepository.cs
      SqlitePlaylistRepository.cs
      SqliteSettingsRepository.cs
    LearnPlayAI.App/              # WPF shell, views, viewmodels
      App.xaml / App.xaml.cs
      Views/
        MainShellWindow.xaml(.cs)
        PlayerView.xaml(.cs)
        TransportBarView.xaml(.cs)
        Panels/
          LibraryPanelView.xaml(.cs)
          RecentPanelView.xaml(.cs)
          PlaylistsPanelView.xaml(.cs)
          SettingsPanelView.xaml(.cs)
      ViewModels/
        PlayerViewModel.cs
        LibraryViewModel.cs
        RecentViewModel.cs
        PlaylistsViewModel.cs
        SettingsViewModel.cs
      Services/
        ThemeManager.cs
      Input/
        KeyboardShortcutManager.cs
      Themes/
        Dark.xaml
        Light.xaml
  tests/
    LearnPlayAI.Core.Tests/       # pure logic, fake repositories, no SQLite
    LearnPlayAI.Data.Tests/       # real SQLite against temp files
```

---

### Task 1: Solution scaffolding

**Files:**
- Create: `LearnPlayAI.sln`, `.gitignore`
- Create: `src/LearnPlayAI.Core/LearnPlayAI.Core.csproj`
- Create: `src/LearnPlayAI.Data/LearnPlayAI.Data.csproj`
- Create: `src/LearnPlayAI.App/LearnPlayAI.App.csproj` (+ default WPF template files)
- Create: `tests/LearnPlayAI.Core.Tests/LearnPlayAI.Core.Tests.csproj`
- Create: `tests/LearnPlayAI.Data.Tests/LearnPlayAI.Data.Tests.csproj`

**Interfaces:**
- Produces: five projects wired into one solution, with project references and NuGet packages in place, building cleanly with `dotnet build`. Every later task assumes these projects and references already exist.

- [ ] **Step 1: Create the solution and five projects**

Run from the repo root (`D:\rea_projects\smart_vlc`):

```bash
dotnet new sln -n LearnPlayAI
dotnet new classlib -n LearnPlayAI.Core -o src/LearnPlayAI.Core -f net8.0
dotnet new classlib -n LearnPlayAI.Data -o src/LearnPlayAI.Data -f net8.0
dotnet new wpf -n LearnPlayAI.App -o src/LearnPlayAI.App -f net8.0-windows
dotnet new xunit -n LearnPlayAI.Core.Tests -o tests/LearnPlayAI.Core.Tests -f net8.0
dotnet new xunit -n LearnPlayAI.Data.Tests -o tests/LearnPlayAI.Data.Tests -f net8.0
```

Delete the template placeholder files that `classlib` generates, since real files replace them in later tasks:

```bash
rm src/LearnPlayAI.Core/Class1.cs
rm src/LearnPlayAI.Data/Class1.cs
```

- [ ] **Step 2: Add projects to the solution**

```bash
dotnet sln add src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
dotnet sln add src/LearnPlayAI.Data/LearnPlayAI.Data.csproj
dotnet sln add src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet sln add tests/LearnPlayAI.Core.Tests/LearnPlayAI.Core.Tests.csproj
dotnet sln add tests/LearnPlayAI.Data.Tests/LearnPlayAI.Data.Tests.csproj
```

- [ ] **Step 3: Wire up project references**

```bash
dotnet add src/LearnPlayAI.Data/LearnPlayAI.Data.csproj reference src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
dotnet add src/LearnPlayAI.App/LearnPlayAI.App.csproj reference src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
dotnet add src/LearnPlayAI.App/LearnPlayAI.App.csproj reference src/LearnPlayAI.Data/LearnPlayAI.Data.csproj
dotnet add tests/LearnPlayAI.Core.Tests/LearnPlayAI.Core.Tests.csproj reference src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
dotnet add tests/LearnPlayAI.Data.Tests/LearnPlayAI.Data.Tests.csproj reference src/LearnPlayAI.Data/LearnPlayAI.Data.csproj
dotnet add tests/LearnPlayAI.Data.Tests/LearnPlayAI.Data.Tests.csproj reference src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
```

- [ ] **Step 4: Add NuGet packages**

```bash
dotnet add src/LearnPlayAI.Core/LearnPlayAI.Core.csproj package LibVLCSharp --version 3.8.2
dotnet add src/LearnPlayAI.Core/LearnPlayAI.Core.csproj package VideoLAN.LibVLC.Windows --version 3.0.20

dotnet add src/LearnPlayAI.Data/LearnPlayAI.Data.csproj package Microsoft.Data.Sqlite --version 8.0.8

dotnet add src/LearnPlayAI.App/LearnPlayAI.App.csproj package LibVLCSharp.WPF --version 3.8.2
dotnet add src/LearnPlayAI.App/LearnPlayAI.App.csproj package CommunityToolkit.Mvvm --version 8.3.2
dotnet add src/LearnPlayAI.App/LearnPlayAI.App.csproj package Microsoft.Extensions.DependencyInjection --version 8.0.0
```

- [ ] **Step 5: Add `.gitignore`**

Create `.gitignore` at the repo root:

```
bin/
obj/
.vs/
*.user
*.db
*.db.corrupt-*.bak
```

- [ ] **Step 6: Build to verify scaffolding**

```bash
dotnet build
```

Expected: `Build succeeded.` with 0 errors (warnings about the two test projects having no tests yet are fine).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Scaffold LearnPlayAI solution: Core, Data, App, and test projects"
```

---

### Task 2: Core domain models, repository interfaces, and shortcut model

**Files:**
- Create: `src/LearnPlayAI.Core/Models/VideoEntry.cs`
- Create: `src/LearnPlayAI.Core/Models/Playlist.cs`
- Create: `src/LearnPlayAI.Core/Models/AppSettings.cs`
- Create: `src/LearnPlayAI.Core/Models/ShortcutBinding.cs`
- Create: `src/LearnPlayAI.Core/Repositories/IVideoLibraryRepository.cs`
- Create: `src/LearnPlayAI.Core/Repositories/IPlaylistRepository.cs`
- Create: `src/LearnPlayAI.Core/Repositories/ISettingsRepository.cs`

**Interfaces:**
- Produces: `VideoEntry`, `Playlist`/`PlaylistItem`, `AppSettings`/`ThemeMode`, `ShortcutBinding` model types, and `IVideoLibraryRepository`, `IPlaylistRepository`, `ISettingsRepository` interfaces — the exact contracts every later Core/Data/App task depends on.

This task has no independent tests of its own (plain data contracts); it is verified by the build and by Task 3's and Task 5–7's tests compiling against these exact types.

- [ ] **Step 1: Write the model types**

`src/LearnPlayAI.Core/Models/VideoEntry.cs`:

```csharp
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
```

`src/LearnPlayAI.Core/Models/Playlist.cs`:

```csharp
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
```

`src/LearnPlayAI.Core/Models/AppSettings.cs`:

```csharp
namespace LearnPlayAI.Core.Models;

public enum ThemeMode
{
    Dark,
    Light
}

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public double DefaultVolume { get; set; } = 0.8;
}
```

`src/LearnPlayAI.Core/Models/ShortcutBinding.cs`:

```csharp
namespace LearnPlayAI.Core.Models;

public sealed class ShortcutBinding
{
    public required string ActionId { get; init; }
    public required string KeyGesture { get; set; }
}
```

- [ ] **Step 2: Write the repository interfaces**

`src/LearnPlayAI.Core/Repositories/IVideoLibraryRepository.cs`:

```csharp
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
```

`src/LearnPlayAI.Core/Repositories/IPlaylistRepository.cs`:

```csharp
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
```

`src/LearnPlayAI.Core/Repositories/ISettingsRepository.cs`:

```csharp
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Repositories;

public interface ISettingsRepository
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync();
    Task SaveShortcutAsync(ShortcutBinding binding);
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
dotnet build src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/LearnPlayAI.Core/Models src/LearnPlayAI.Core/Repositories
git commit -m "Add Core domain models and repository interfaces"
```

---

### Task 3: VideoHasher (content-based video identity)

**Files:**
- Create: `src/LearnPlayAI.Core/Hashing/IVideoHasher.cs`
- Create: `src/LearnPlayAI.Core/Hashing/VideoHasher.cs`
- Test: `tests/LearnPlayAI.Core.Tests/Hashing/VideoHasherTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `IVideoHasher.ComputeHash(string filePath) : string`, implemented by `VideoHasher`. Task 8 (`LibraryService`) consumes this directly.

- [ ] **Step 1: Write the failing tests**

`tests/LearnPlayAI.Core.Tests/Hashing/VideoHasherTests.cs`:

```csharp
using LearnPlayAI.Core.Hashing;

namespace LearnPlayAI.Core.Tests.Hashing;

public class VideoHasherTests
{
    [Fact]
    public void ComputeHash_SameContent_ProducesSameHash()
    {
        var hasher = new VideoHasher();
        var path1 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });
        var path2 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });

        try
        {
            Assert.Equal(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ComputeHash_DifferentContent_ProducesDifferentHash()
    {
        var hasher = new VideoHasher();
        var path1 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });
        var path2 = WriteTempFile(new byte[] { 1, 2, 3, 4, 6 });

        try
        {
            Assert.NotEqual(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ComputeHash_LargeFile_IgnoresChangesOutsideSampledRegion()
    {
        // The hasher samples the first and last 1 MB plus file length, to avoid
        // hashing entire large video files. A 3 MB file has an un-sampled 1 MB
        // region in the middle; changes there are intentionally not detected.
        var hasher = new VideoHasher();
        const int size = 3 * 1024 * 1024;
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);

        var path1 = WriteTempFile(bytes);
        var modified = (byte[])bytes.Clone();
        modified[size / 2] = (byte)(modified[size / 2] + 1);
        var path2 = WriteTempFile(modified);

        try
        {
            Assert.Equal(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter VideoHasherTests
```

Expected: FAIL — `VideoHasher` does not exist yet (compile error).

- [ ] **Step 3: Write the implementation**

`src/LearnPlayAI.Core/Hashing/IVideoHasher.cs`:

```csharp
namespace LearnPlayAI.Core.Hashing;

public interface IVideoHasher
{
    string ComputeHash(string filePath);
}
```

`src/LearnPlayAI.Core/Hashing/VideoHasher.cs`:

```csharp
using System.Security.Cryptography;

namespace LearnPlayAI.Core.Hashing;

public sealed class VideoHasher : IVideoHasher
{
    private const int SampleSizeBytes = 1 * 1024 * 1024;

    public string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        long length = stream.Length;

        using var sha256 = SHA256.Create();
        using var hashStream = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write);

        var lengthBytes = BitConverter.GetBytes(length);
        hashStream.Write(lengthBytes, 0, lengthBytes.Length);

        int headSize = (int)Math.Min(SampleSizeBytes, length);
        var headBuffer = new byte[headSize];
        ReadExact(stream, headBuffer, headSize);
        hashStream.Write(headBuffer, 0, headSize);

        if (length > SampleSizeBytes)
        {
            int tailSize = (int)Math.Min(SampleSizeBytes, length);
            stream.Seek(-tailSize, SeekOrigin.End);
            var tailBuffer = new byte[tailSize];
            ReadExact(stream, tailBuffer, tailSize);
            hashStream.Write(tailBuffer, 0, tailSize);
        }

        hashStream.FlushFinalBlock();
        return Convert.ToHexString(sha256.Hash!);
    }

    private static void ReadExact(Stream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            totalRead += read;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter VideoHasherTests
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LearnPlayAI.Core/Hashing tests/LearnPlayAI.Core.Tests/Hashing
git commit -m "Add content-based VideoHasher for stable video identity"
```

---

### Task 4: SQLite schema and DatabaseBootstrapper (with corruption fallback)

**Files:**
- Create: `src/LearnPlayAI.Data/DatabaseBootstrapper.cs`
- Test: `tests/LearnPlayAI.Data.Tests/DatabaseBootstrapperTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks (uses `Microsoft.Data.Sqlite` directly).
- Produces: `DatabaseBootstrapper.OpenDatabase(string dbPath) : SqliteConnection` — an open connection, schema guaranteed present, `PRAGMA foreign_keys = ON` set. Tasks 5–7 (repositories) call this on every operation. Tables created: `video_entries`, `playlists`, `playlist_items`, `app_settings`, `shortcut_bindings`.

- [ ] **Step 1: Write the failing tests**

`tests/LearnPlayAI.Data.Tests/DatabaseBootstrapperTests.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data.Tests;

public class DatabaseBootstrapperTests
{
    [Fact]
    public void OpenDatabase_NewFile_CreatesAllTables()
    {
        var path = TempDbPath();
        try
        {
            using var connection = DatabaseBootstrapper.OpenDatabase(path);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
            using var reader = command.ExecuteReader();
            var tables = new List<string>();
            while (reader.Read()) tables.Add(reader.GetString(0));

            Assert.Contains("video_entries", tables);
            Assert.Contains("playlists", tables);
            Assert.Contains("playlist_items", tables);
            Assert.Contains("app_settings", tables);
            Assert.Contains("shortcut_bindings", tables);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void OpenDatabase_ReopeningExistingFile_Succeeds()
    {
        var path = TempDbPath();
        try
        {
            using (var first = DatabaseBootstrapper.OpenDatabase(path)) { }
            using var second = DatabaseBootstrapper.OpenDatabase(path);
            Assert.Equal(System.Data.ConnectionState.Open, second.State);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public void OpenDatabase_CorruptFile_BacksUpAndCreatesFreshDatabase()
    {
        var path = TempDbPath();
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03, 0x04 }); // not a valid SQLite file
        try
        {
            using var connection = DatabaseBootstrapper.OpenDatabase(path);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM video_entries;";
            var count = (long)command.ExecuteScalar()!;
            Assert.Equal(0, count);

            var backupExists = Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*.bak").Length == 1;
            Assert.True(backupExists);
        }
        finally
        {
            SafeDelete(path);
            foreach (var backup in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*.bak"))
            {
                SafeDelete(backup);
            }
        }
    }

    private static string TempDbPath() => Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    private static void SafeDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter DatabaseBootstrapperTests
```

Expected: FAIL — `DatabaseBootstrapper` does not exist yet.

- [ ] **Step 3: Write the implementation**

`src/LearnPlayAI.Data/DatabaseBootstrapper.cs`:

```csharp
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data;

public static class DatabaseBootstrapper
{
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS video_entries (
            video_id TEXT PRIMARY KEY,
            file_path TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            title TEXT NOT NULL,
            duration_ticks INTEGER NOT NULL DEFAULT 0,
            last_position_ticks INTEGER NOT NULL DEFAULT 0,
            last_watched_utc TEXT NULL,
            added_utc TEXT NOT NULL,
            is_missing INTEGER NOT NULL DEFAULT 0
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_video_entries_content_hash ON video_entries(content_hash);

        CREATE TABLE IF NOT EXISTS playlists (
            playlist_id TEXT PRIMARY KEY,
            name TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS playlist_items (
            playlist_id TEXT NOT NULL,
            video_id TEXT NOT NULL,
            position INTEGER NOT NULL,
            PRIMARY KEY (playlist_id, video_id)
        );

        CREATE TABLE IF NOT EXISTS app_settings (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS shortcut_bindings (
            action_id TEXT PRIMARY KEY,
            key_gesture TEXT NOT NULL
        );
        """;

    public static SqliteConnection OpenDatabase(string dbPath)
    {
        EnsureUsableDatabaseFile(dbPath);

        var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        using (var createSchema = connection.CreateCommand())
        {
            createSchema.CommandText = Schema;
            createSchema.ExecuteNonQuery();
        }

        return connection;
    }

    private static void EnsureUsableDatabaseFile(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            var result = (string)check.ExecuteScalar()!;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new SqliteException("quick_check failed", 11);
            }
        }
        catch (SqliteException)
        {
            var backupPath = $"{dbPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak";
            File.Copy(dbPath, backupPath, overwrite: true);
            File.Delete(dbPath);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter DatabaseBootstrapperTests
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LearnPlayAI.Data/DatabaseBootstrapper.cs tests/LearnPlayAI.Data.Tests/DatabaseBootstrapperTests.cs
git commit -m "Add SQLite schema bootstrap with corrupt-database fallback"
```

---

### Task 5: SqliteVideoLibraryRepository

**Files:**
- Create: `src/LearnPlayAI.Data/SqliteVideoLibraryRepository.cs`
- Test: `tests/LearnPlayAI.Data.Tests/SqliteVideoLibraryRepositoryTests.cs`

**Interfaces:**
- Consumes: `IVideoLibraryRepository` (Task 2), `DatabaseBootstrapper.OpenDatabase` (Task 4), `VideoEntry` (Task 2).
- Produces: `SqliteVideoLibraryRepository(string dbPath)` implementing `IVideoLibraryRepository`. Task 8 (`LibraryService`) and Task 12 (App composition root) consume this.

- [ ] **Step 1: Write the failing tests**

`tests/LearnPlayAI.Data.Tests/SqliteVideoLibraryRepositoryTests.cs`:

```csharp
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Data.Tests;

public class SqliteVideoLibraryRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task UpsertAndFindById_RoundTrips()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-1");

        await repo.UpsertAsync(entry);
        var found = await repo.FindByIdAsync(entry.VideoId);

        Assert.NotNull(found);
        Assert.Equal(entry.FilePath, found!.FilePath);
        Assert.Equal(entry.ContentHash, found.ContentHash);
        Assert.Equal(entry.Title, found.Title);
    }

    [Fact]
    public async Task FindByHash_ReturnsMatchingEntry()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-2");
        await repo.UpsertAsync(entry);

        var found = await repo.FindByHashAsync("hash-2");

        Assert.NotNull(found);
        Assert.Equal(entry.VideoId, found!.VideoId);
    }

    [Fact]
    public async Task FindByHash_NoMatch_ReturnsNull()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        Assert.Null(await repo.FindByHashAsync("does-not-exist"));
    }

    [Fact]
    public async Task UpdatePositionAsync_PersistsPositionAndWatchedTime()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-3");
        await repo.UpsertAsync(entry);

        var watchedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await repo.UpdatePositionAsync(entry.VideoId, TimeSpan.FromMinutes(5), watchedUtc);

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(5), found!.LastPosition);
        Assert.Equal(watchedUtc, found.LastWatchedUtc);
    }

    [Fact]
    public async Task UpdateDurationAsync_PersistsDuration()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-4");
        await repo.UpsertAsync(entry);

        await repo.UpdateDurationAsync(entry.VideoId, TimeSpan.FromMinutes(42));

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.Equal(TimeSpan.FromMinutes(42), found!.Duration);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersByLastWatchedDescending_AndExcludesNeverWatched()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var neverWatched = NewEntry(hash: "hash-5");
        var watchedOld = NewEntry(hash: "hash-6");
        var watchedNew = NewEntry(hash: "hash-7");
        await repo.UpsertAsync(neverWatched);
        await repo.UpsertAsync(watchedOld);
        await repo.UpsertAsync(watchedNew);

        await repo.UpdatePositionAsync(watchedOld.VideoId, TimeSpan.FromMinutes(1), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await repo.UpdatePositionAsync(watchedNew.VideoId, TimeSpan.FromMinutes(1), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var recent = await repo.GetRecentAsync(limit: 10);

        Assert.Equal(2, recent.Count);
        Assert.Equal(watchedNew.VideoId, recent[0].VideoId);
        Assert.Equal(watchedOld.VideoId, recent[1].VideoId);
    }

    [Fact]
    public async Task SetMissingAsync_PersistsFlag()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-8");
        await repo.UpsertAsync(entry);

        await repo.SetMissingAsync(entry.VideoId, true);

        var found = await repo.FindByIdAsync(entry.VideoId);
        Assert.True(found!.IsMissing);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var repo = new SqliteVideoLibraryRepository(_dbPath);
        var entry = NewEntry(hash: "hash-9");
        await repo.UpsertAsync(entry);

        await repo.DeleteAsync(entry.VideoId);

        Assert.Null(await repo.FindByIdAsync(entry.VideoId));
    }

    private static VideoEntry NewEntry(string hash) => new()
    {
        VideoId = Guid.NewGuid(),
        FilePath = @"C:\videos\sample.mp4",
        ContentHash = hash,
        Title = "Sample",
        AddedUtc = DateTime.UtcNow
    };

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqliteVideoLibraryRepositoryTests
```

Expected: FAIL — `SqliteVideoLibraryRepository` does not exist yet.

- [ ] **Step 3: Write the implementation**

`src/LearnPlayAI.Data/SqliteVideoLibraryRepository.cs`:

```csharp
using System.Globalization;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data;

public sealed class SqliteVideoLibraryRepository : IVideoLibraryRepository
{
    private readonly string _dbPath;

    public SqliteVideoLibraryRepository(string dbPath) => _dbPath = dbPath;

    public async Task<VideoEntry?> FindByIdAsync(Guid videoId)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM video_entries WHERE video_id = $id;";
        command.Parameters.AddWithValue("$id", videoId.ToString());
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadEntry(reader) : null;
    }

    public async Task<VideoEntry?> FindByHashAsync(string contentHash)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM video_entries WHERE content_hash = $hash;";
        command.Parameters.AddWithValue("$hash", contentHash);
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadEntry(reader) : null;
    }

    public async Task UpsertAsync(VideoEntry entry)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO video_entries
                (video_id, file_path, content_hash, title, duration_ticks, last_position_ticks, last_watched_utc, added_utc, is_missing)
            VALUES
                ($id, $path, $hash, $title, $duration, $position, $watched, $added, $missing)
            ON CONFLICT(video_id) DO UPDATE SET
                file_path = $path,
                title = $title,
                duration_ticks = $duration,
                last_position_ticks = $position,
                last_watched_utc = $watched,
                is_missing = $missing;
            """;
        command.Parameters.AddWithValue("$id", entry.VideoId.ToString());
        command.Parameters.AddWithValue("$path", entry.FilePath);
        command.Parameters.AddWithValue("$hash", entry.ContentHash);
        command.Parameters.AddWithValue("$title", entry.Title);
        command.Parameters.AddWithValue("$duration", entry.Duration.Ticks);
        command.Parameters.AddWithValue("$position", entry.LastPosition.Ticks);
        command.Parameters.AddWithValue("$watched", (object?)entry.LastWatchedUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$added", entry.AddedUtc.ToString("O"));
        command.Parameters.AddWithValue("$missing", entry.IsMissing ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<VideoEntry>> GetRecentAsync(int limit)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM video_entries
            WHERE last_watched_utc IS NOT NULL
            ORDER BY last_watched_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = await command.ExecuteReaderAsync();
        var results = new List<VideoEntry>();
        while (await reader.ReadAsync()) results.Add(ReadEntry(reader));
        return results;
    }

    public async Task<IReadOnlyList<VideoEntry>> GetAllAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM video_entries ORDER BY added_utc DESC;";
        using var reader = await command.ExecuteReaderAsync();
        var results = new List<VideoEntry>();
        while (await reader.ReadAsync()) results.Add(ReadEntry(reader));
        return results;
    }

    public async Task UpdatePositionAsync(Guid videoId, TimeSpan position, DateTime watchedUtc)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE video_entries SET last_position_ticks = $pos, last_watched_utc = $watched WHERE video_id = $id;";
        command.Parameters.AddWithValue("$pos", position.Ticks);
        command.Parameters.AddWithValue("$watched", watchedUtc.ToString("O"));
        command.Parameters.AddWithValue("$id", videoId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateDurationAsync(Guid videoId, TimeSpan duration)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE video_entries SET duration_ticks = $duration WHERE video_id = $id;";
        command.Parameters.AddWithValue("$duration", duration.Ticks);
        command.Parameters.AddWithValue("$id", videoId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetMissingAsync(Guid videoId, bool isMissing)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE video_entries SET is_missing = $missing WHERE video_id = $id;";
        command.Parameters.AddWithValue("$missing", isMissing ? 1 : 0);
        command.Parameters.AddWithValue("$id", videoId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid videoId)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM video_entries WHERE video_id = $id;";
        command.Parameters.AddWithValue("$id", videoId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static VideoEntry ReadEntry(SqliteDataReader reader)
    {
        return new VideoEntry
        {
            VideoId = Guid.Parse(reader.GetString(reader.GetOrdinal("video_id"))),
            FilePath = reader.GetString(reader.GetOrdinal("file_path")),
            ContentHash = reader.GetString(reader.GetOrdinal("content_hash")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Duration = TimeSpan.FromTicks(reader.GetInt64(reader.GetOrdinal("duration_ticks"))),
            LastPosition = TimeSpan.FromTicks(reader.GetInt64(reader.GetOrdinal("last_position_ticks"))),
            LastWatchedUtc = reader.IsDBNull(reader.GetOrdinal("last_watched_utc"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_watched_utc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            AddedUtc = DateTime.Parse(reader.GetString(reader.GetOrdinal("added_utc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            IsMissing = reader.GetInt32(reader.GetOrdinal("is_missing")) == 1
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqliteVideoLibraryRepositoryTests
```

Expected: PASS (8 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LearnPlayAI.Data/SqliteVideoLibraryRepository.cs tests/LearnPlayAI.Data.Tests/SqliteVideoLibraryRepositoryTests.cs
git commit -m "Add SQLite-backed video library repository"
```

---

### Task 6: SqlitePlaylistRepository

**Files:**
- Create: `src/LearnPlayAI.Data/SqlitePlaylistRepository.cs`
- Test: `tests/LearnPlayAI.Data.Tests/SqlitePlaylistRepositoryTests.cs`

**Interfaces:**
- Consumes: `IPlaylistRepository` (Task 2), `DatabaseBootstrapper.OpenDatabase` (Task 4).
- Produces: `SqlitePlaylistRepository(string dbPath)` implementing `IPlaylistRepository`. Task 9 (`PlaylistService`) and Task 12 (App composition root) consume this.

- [ ] **Step 1: Write the failing tests**

`tests/LearnPlayAI.Data.Tests/SqlitePlaylistRepositoryTests.cs`:

```csharp
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
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqlitePlaylistRepositoryTests
```

Expected: FAIL — `SqlitePlaylistRepository` does not exist yet.

- [ ] **Step 3: Write the implementation**

`src/LearnPlayAI.Data/SqlitePlaylistRepository.cs`:

```csharp
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Data;

public sealed class SqlitePlaylistRepository : IPlaylistRepository
{
    private readonly string _dbPath;

    public SqlitePlaylistRepository(string dbPath) => _dbPath = dbPath;

    public async Task<Playlist> CreateAsync(string name)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var playlistId = Guid.NewGuid();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO playlists (playlist_id, name) VALUES ($id, $name);";
        command.Parameters.AddWithValue("$id", playlistId.ToString());
        command.Parameters.AddWithValue("$name", name);
        await command.ExecuteNonQueryAsync();
        return new Playlist { PlaylistId = playlistId, Name = name };
    }

    public async Task RenameAsync(Guid playlistId, string newName)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE playlists SET name = $name WHERE playlist_id = $id;";
        command.Parameters.AddWithValue("$name", newName);
        command.Parameters.AddWithValue("$id", playlistId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid playlistId)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using (var deleteItems = connection.CreateCommand())
        {
            deleteItems.CommandText = "DELETE FROM playlist_items WHERE playlist_id = $id;";
            deleteItems.Parameters.AddWithValue("$id", playlistId.ToString());
            await deleteItems.ExecuteNonQueryAsync();
        }
        using var deletePlaylist = connection.CreateCommand();
        deletePlaylist.CommandText = "DELETE FROM playlists WHERE playlist_id = $id;";
        deletePlaylist.Parameters.AddWithValue("$id", playlistId.ToString());
        await deletePlaylist.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<Playlist>> GetAllAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var playlists = new Dictionary<Guid, Playlist>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT playlist_id, name FROM playlists;";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = Guid.Parse(reader.GetString(0));
                playlists[id] = new Playlist { PlaylistId = id, Name = reader.GetString(1) };
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT playlist_id, video_id, position FROM playlist_items ORDER BY position;";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var playlistId = Guid.Parse(reader.GetString(0));
                if (playlists.TryGetValue(playlistId, out var playlist))
                {
                    playlist.Items.Add(new PlaylistItem
                    {
                        VideoId = Guid.Parse(reader.GetString(1)),
                        Position = reader.GetInt32(2)
                    });
                }
            }
        }

        return playlists.Values.ToList();
    }

    public async Task<Playlist?> GetByIdAsync(Guid playlistId)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(p => p.PlaylistId == playlistId);
    }

    public async Task AddItemAsync(Guid playlistId, Guid videoId)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        long nextPosition;
        using (var maxCommand = connection.CreateCommand())
        {
            maxCommand.CommandText = "SELECT COALESCE(MAX(position), -1) + 1 FROM playlist_items WHERE playlist_id = $id;";
            maxCommand.Parameters.AddWithValue("$id", playlistId.ToString());
            nextPosition = (long)(await maxCommand.ExecuteScalarAsync())!;
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO playlist_items (playlist_id, video_id, position) VALUES ($pid, $vid, $pos);";
        insert.Parameters.AddWithValue("$pid", playlistId.ToString());
        insert.Parameters.AddWithValue("$vid", videoId.ToString());
        insert.Parameters.AddWithValue("$pos", nextPosition);
        await insert.ExecuteNonQueryAsync();
    }

    public async Task RemoveItemAsync(Guid playlistId, Guid videoId)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM playlist_items WHERE playlist_id = $pid AND video_id = $vid;";
        command.Parameters.AddWithValue("$pid", playlistId.ToString());
        command.Parameters.AddWithValue("$vid", videoId.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqlitePlaylistRepositoryTests
```

Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LearnPlayAI.Data/SqlitePlaylistRepository.cs tests/LearnPlayAI.Data.Tests/SqlitePlaylistRepositoryTests.cs
git commit -m "Add SQLite-backed playlist repository"
```

---

### Task 7: SqliteSettingsRepository

**Files:**
- Create: `src/LearnPlayAI.Data/SqliteSettingsRepository.cs`
- Test: `tests/LearnPlayAI.Data.Tests/SqliteSettingsRepositoryTests.cs`

**Interfaces:**
- Consumes: `ISettingsRepository` (Task 2), `DatabaseBootstrapper.OpenDatabase` (Task 4).
- Produces: `SqliteSettingsRepository(string dbPath)` implementing `ISettingsRepository`. Task 10 (`SettingsService`) and Task 12 (App composition root) consume this.

- [ ] **Step 1: Write the failing tests**

`tests/LearnPlayAI.Data.Tests/SqliteSettingsRepositoryTests.cs`:

```csharp
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Data.Tests;

public class SqliteSettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"learnplay-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task LoadSettingsAsync_NoStoredValues_ReturnsDefaults()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        var settings = await repo.LoadSettingsAsync();

        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal(0.8, settings.DefaultVolume);
    }

    [Fact]
    public async Task SaveThenLoadSettingsAsync_RoundTrips()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveSettingsAsync(new AppSettings { Theme = ThemeMode.Light, DefaultVolume = 0.45 });

        var loaded = await repo.LoadSettingsAsync();

        Assert.Equal(ThemeMode.Light, loaded.Theme);
        Assert.Equal(0.45, loaded.DefaultVolume);
    }

    [Fact]
    public async Task SaveShortcutAsync_NewBinding_IsPersisted()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "Space" });

        var bindings = await repo.LoadShortcutsAsync();

        Assert.Single(bindings);
        Assert.Equal("Space", bindings[0].KeyGesture);
    }

    [Fact]
    public async Task SaveShortcutAsync_ExistingAction_OverwritesGesture()
    {
        var repo = new SqliteSettingsRepository(_dbPath);
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "Space" });
        await repo.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "P" });

        var bindings = await repo.LoadShortcutsAsync();

        Assert.Single(bindings);
        Assert.Equal("P", bindings[0].KeyGesture);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqliteSettingsRepositoryTests
```

Expected: FAIL — `SqliteSettingsRepository` does not exist yet.

- [ ] **Step 3: Write the implementation**

`src/LearnPlayAI.Data/SqliteSettingsRepository.cs`:

```csharp
using System.Globalization;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace LearnPlayAI.Data;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly string _dbPath;

    public SqliteSettingsRepository(string dbPath) => _dbPath = dbPath;

    public async Task<AppSettings> LoadSettingsAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var settings = new AppSettings();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM app_settings;";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            switch (key)
            {
                case "Theme":
                    settings.Theme = Enum.Parse<ThemeMode>(value);
                    break;
                case "DefaultVolume":
                    settings.DefaultVolume = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
            }
        }

        return settings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        await UpsertSettingAsync(connection, "Theme", settings.Theme.ToString());
        await UpsertSettingAsync(connection, "DefaultVolume", settings.DefaultVolume.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync()
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        var bindings = new List<ShortcutBinding>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT action_id, key_gesture FROM shortcut_bindings;";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bindings.Add(new ShortcutBinding { ActionId = reader.GetString(0), KeyGesture = reader.GetString(1) });
        }
        return bindings;
    }

    public async Task SaveShortcutAsync(ShortcutBinding binding)
    {
        using var connection = DatabaseBootstrapper.OpenDatabase(_dbPath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO shortcut_bindings (action_id, key_gesture) VALUES ($action, $gesture)
            ON CONFLICT(action_id) DO UPDATE SET key_gesture = $gesture;
            """;
        command.Parameters.AddWithValue("$action", binding.ActionId);
        command.Parameters.AddWithValue("$gesture", binding.KeyGesture);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task UpsertSettingAsync(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_settings (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = $value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Data.Tests --filter SqliteSettingsRepositoryTests
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/LearnPlayAI.Data/SqliteSettingsRepository.cs tests/LearnPlayAI.Data.Tests/SqliteSettingsRepositoryTests.cs
git commit -m "Add SQLite-backed settings and shortcut-binding repository"
```

---

### Task 8: LibraryService (with in-memory fake repository)

**Files:**
- Create: `tests/LearnPlayAI.Core.Tests/Fakes/FakeVideoLibraryRepository.cs`
- Create: `tests/LearnPlayAI.Core.Tests/Fakes/StubVideoHasher.cs`
- Create: `src/LearnPlayAI.Core/Services/LibraryService.cs`
- Test: `tests/LearnPlayAI.Core.Tests/Services/LibraryServiceTests.cs`

**Interfaces:**
- Consumes: `IVideoLibraryRepository`, `IVideoHasher` (Task 2, Task 3).
- Produces: `LibraryService(IVideoLibraryRepository, IVideoHasher)` with `RegisterFileAsync(string filePath) : Task<VideoEntry>`, `GetRecentAsync(int limit = 20)`, `GetLibraryAsync()`, `GetByIdAsync(Guid)`, `UpdatePlaybackPositionAsync(Guid, TimeSpan)`, `UpdateDurationAsync(Guid, TimeSpan)`, `MarkMissingAsync(Guid)`, `RemoveAsync(Guid)`. Consumed by Task 12 (App composition root) and the Library/Recent/Player view models (Tasks 14, 18, 19).

- [ ] **Step 1: Write the fakes the test needs**

`tests/LearnPlayAI.Core.Tests/Fakes/FakeVideoLibraryRepository.cs`:

```csharp
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
```

`tests/LearnPlayAI.Core.Tests/Fakes/StubVideoHasher.cs`:

```csharp
using LearnPlayAI.Core.Hashing;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class StubVideoHasher : IVideoHasher
{
    private readonly Dictionary<string, string> _hashesByPath;

    public StubVideoHasher(Dictionary<string, string>? hashesByPath = null) =>
        _hashesByPath = hashesByPath ?? new Dictionary<string, string>();

    public string ComputeHash(string filePath) =>
        _hashesByPath.TryGetValue(filePath, out var hash) ? hash : filePath;
}
```

- [ ] **Step 2: Write the failing tests**

`tests/LearnPlayAI.Core.Tests/Services/LibraryServiceTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter LibraryServiceTests
```

Expected: FAIL — `LibraryService` does not exist yet.

- [ ] **Step 4: Write the implementation**

`src/LearnPlayAI.Core/Services/LibraryService.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter LibraryServiceTests
```

Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/LearnPlayAI.Core/Services/LibraryService.cs tests/LearnPlayAI.Core.Tests/Services/LibraryServiceTests.cs tests/LearnPlayAI.Core.Tests/Fakes
git commit -m "Add LibraryService with content-hash-based dedup and missing-file handling"
```

---

### Task 9: PlaylistService (with in-memory fake repository)

**Files:**
- Create: `tests/LearnPlayAI.Core.Tests/Fakes/FakePlaylistRepository.cs`
- Create: `src/LearnPlayAI.Core/Services/PlaylistService.cs`
- Test: `tests/LearnPlayAI.Core.Tests/Services/PlaylistServiceTests.cs`

**Interfaces:**
- Consumes: `IPlaylistRepository` (Task 2).
- Produces: `PlaylistService(IPlaylistRepository)` with `CreatePlaylistAsync`, `RenamePlaylistAsync`, `DeletePlaylistAsync`, `GetPlaylistsAsync`, `GetPlaylistAsync`, `AddVideoAsync`, `RemoveVideoAsync`. Consumed by Task 12 (App composition root) and Task 19 (`PlaylistsViewModel`).

- [ ] **Step 1: Write the fake the test needs**

`tests/LearnPlayAI.Core.Tests/Fakes/FakePlaylistRepository.cs`:

```csharp
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class FakePlaylistRepository : IPlaylistRepository
{
    private readonly Dictionary<Guid, Playlist> _playlists = new();

    public Task<Playlist> CreateAsync(string name)
    {
        var playlist = new Playlist { PlaylistId = Guid.NewGuid(), Name = name };
        _playlists[playlist.PlaylistId] = playlist;
        return Task.FromResult(playlist);
    }

    public Task RenameAsync(Guid playlistId, string newName)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist)) playlist.Name = newName;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid playlistId)
    {
        _playlists.Remove(playlistId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Playlist>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Playlist>>(_playlists.Values.ToList());

    public Task<Playlist?> GetByIdAsync(Guid playlistId) =>
        Task.FromResult(_playlists.TryGetValue(playlistId, out var playlist) ? playlist : null);

    public Task AddItemAsync(Guid playlistId, Guid videoId)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist))
        {
            var nextPosition = playlist.Items.Count == 0 ? 0 : playlist.Items.Max(i => i.Position) + 1;
            playlist.Items.Add(new PlaylistItem { VideoId = videoId, Position = nextPosition });
        }
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(Guid playlistId, Guid videoId)
    {
        if (_playlists.TryGetValue(playlistId, out var playlist))
        {
            playlist.Items.RemoveAll(i => i.VideoId == videoId);
        }
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/LearnPlayAI.Core.Tests/Services/PlaylistServiceTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter PlaylistServiceTests
```

Expected: FAIL — `PlaylistService` does not exist yet.

- [ ] **Step 4: Write the implementation**

`src/LearnPlayAI.Core/Services/PlaylistService.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter PlaylistServiceTests
```

Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/LearnPlayAI.Core/Services/PlaylistService.cs tests/LearnPlayAI.Core.Tests/Services/PlaylistServiceTests.cs tests/LearnPlayAI.Core.Tests/Fakes/FakePlaylistRepository.cs
git commit -m "Add PlaylistService"
```

---

### Task 10: SettingsService, DefaultShortcuts (with in-memory fake repository)

**Files:**
- Create: `tests/LearnPlayAI.Core.Tests/Fakes/FakeSettingsRepository.cs`
- Create: `src/LearnPlayAI.Core/Services/DefaultShortcuts.cs`
- Create: `src/LearnPlayAI.Core/Services/SettingsService.cs`
- Test: `tests/LearnPlayAI.Core.Tests/Services/SettingsServiceTests.cs`

**Interfaces:**
- Consumes: `ISettingsRepository` (Task 2).
- Produces: `DefaultShortcuts.Create() : IReadOnlyList<ShortcutBinding>`; `SettingsService(ISettingsRepository)` exposing `Current : AppSettings`, `Shortcuts : IReadOnlyList<ShortcutBinding>`, `InitializeAsync()`, `SetThemeAsync(ThemeMode)`, `SetDefaultVolumeAsync(double)`, `SetShortcutAsync(string actionId, string keyGesture)`. Consumed by Task 12 (App composition root), Task 13 (`ThemeManager`/theme init), Task 20 (`SettingsViewModel`), Task 21 (`KeyboardShortcutManager`).

Default shortcut action IDs defined here (used verbatim later by `KeyboardShortcutManager` in Task 21 and the player commands in Task 15): `PlayPause`, `Stop`, `SeekForward`, `SeekBackward`, `VolumeUp`, `VolumeDown`, `ToggleMute`, `ToggleFullScreen`, `StepFrame`, `Screenshot`, `SpeedUp`, `SpeedDown`.

- [ ] **Step 1: Write the fake the test needs**

`tests/LearnPlayAI.Core.Tests/Fakes/FakeSettingsRepository.cs`:

```csharp
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class FakeSettingsRepository : ISettingsRepository
{
    private AppSettings _settings = new();
    private readonly Dictionary<string, ShortcutBinding> _shortcuts = new();

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(_settings);

    public Task SaveSettingsAsync(AppSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShortcutBinding>> LoadShortcutsAsync() =>
        Task.FromResult<IReadOnlyList<ShortcutBinding>>(_shortcuts.Values.ToList());

    public Task SaveShortcutAsync(ShortcutBinding binding)
    {
        _shortcuts[binding.ActionId] = binding;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/LearnPlayAI.Core.Tests/Services/SettingsServiceTests.cs`:

```csharp
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using LearnPlayAI.Core.Tests.Fakes;

namespace LearnPlayAI.Core.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public async Task InitializeAsync_NoStoredShortcuts_SeedsDefaults()
    {
        var service = new SettingsService(new FakeSettingsRepository());

        await service.InitializeAsync();

        Assert.Equal(DefaultShortcuts.Create().Count, service.Shortcuts.Count);
        Assert.Contains(service.Shortcuts, s => s.ActionId == "PlayPause");
    }

    [Fact]
    public async Task InitializeAsync_StoredShortcuts_UsesStoredInsteadOfDefaults()
    {
        var repository = new FakeSettingsRepository();
        await repository.SaveShortcutAsync(new ShortcutBinding { ActionId = "PlayPause", KeyGesture = "P" });
        var service = new SettingsService(repository);

        await service.InitializeAsync();

        Assert.Single(service.Shortcuts);
        Assert.Equal("P", service.Shortcuts[0].KeyGesture);
    }

    [Fact]
    public async Task SetThemeAsync_UpdatesCurrentAndPersists()
    {
        var repository = new FakeSettingsRepository();
        var service = new SettingsService(repository);
        await service.InitializeAsync();

        await service.SetThemeAsync(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, service.Current.Theme);
        Assert.Equal(ThemeMode.Light, (await repository.LoadSettingsAsync()).Theme);
    }

    [Fact]
    public async Task SetShortcutAsync_ExistingAction_UpdatesGesture()
    {
        var service = new SettingsService(new FakeSettingsRepository());
        await service.InitializeAsync();

        await service.SetShortcutAsync("PlayPause", "P");

        Assert.Equal("P", service.Shortcuts.Single(s => s.ActionId == "PlayPause").KeyGesture);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter SettingsServiceTests
```

Expected: FAIL — `SettingsService`/`DefaultShortcuts` do not exist yet.

- [ ] **Step 4: Write the implementation**

`src/LearnPlayAI.Core/Services/DefaultShortcuts.cs`:

```csharp
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Services;

public static class DefaultShortcuts
{
    public static IReadOnlyList<ShortcutBinding> Create() => new List<ShortcutBinding>
    {
        new() { ActionId = "PlayPause", KeyGesture = "Space" },
        new() { ActionId = "Stop", KeyGesture = "Ctrl+Space" },
        new() { ActionId = "SeekForward", KeyGesture = "Right" },
        new() { ActionId = "SeekBackward", KeyGesture = "Left" },
        new() { ActionId = "VolumeUp", KeyGesture = "Up" },
        new() { ActionId = "VolumeDown", KeyGesture = "Down" },
        new() { ActionId = "ToggleMute", KeyGesture = "M" },
        new() { ActionId = "ToggleFullScreen", KeyGesture = "F" },
        new() { ActionId = "StepFrame", KeyGesture = "OemPeriod" },
        new() { ActionId = "Screenshot", KeyGesture = "Ctrl+Shift+S" },
        new() { ActionId = "SpeedUp", KeyGesture = "OemPlus" },
        new() { ActionId = "SpeedDown", KeyGesture = "OemMinus" },
    };
}
```

`src/LearnPlayAI.Core/Services/SettingsService.cs`:

```csharp
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Repositories;

namespace LearnPlayAI.Core.Services;

public sealed class SettingsService
{
    private readonly ISettingsRepository _repository;

    public AppSettings Current { get; private set; } = new();
    public IReadOnlyList<ShortcutBinding> Shortcuts { get; private set; } = new List<ShortcutBinding>();

    public SettingsService(ISettingsRepository repository) => _repository = repository;

    public async Task InitializeAsync()
    {
        Current = await _repository.LoadSettingsAsync();

        var stored = await _repository.LoadShortcutsAsync();
        if (stored.Count == 0)
        {
            var defaults = DefaultShortcuts.Create();
            foreach (var binding in defaults)
            {
                await _repository.SaveShortcutAsync(binding);
            }
            Shortcuts = defaults;
        }
        else
        {
            Shortcuts = stored;
        }
    }

    public async Task SetThemeAsync(ThemeMode mode)
    {
        Current.Theme = mode;
        await _repository.SaveSettingsAsync(Current);
    }

    public async Task SetDefaultVolumeAsync(double volume)
    {
        Current.DefaultVolume = volume;
        await _repository.SaveSettingsAsync(Current);
    }

    public async Task SetShortcutAsync(string actionId, string keyGesture)
    {
        var existing = Shortcuts.FirstOrDefault(s => s.ActionId == actionId);
        if (existing is null)
        {
            existing = new ShortcutBinding { ActionId = actionId, KeyGesture = keyGesture };
            Shortcuts = Shortcuts.Append(existing).ToList();
        }
        else
        {
            existing.KeyGesture = keyGesture;
        }
        await _repository.SaveShortcutAsync(existing);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter SettingsServiceTests
```

Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/LearnPlayAI.Core/Services/DefaultShortcuts.cs src/LearnPlayAI.Core/Services/SettingsService.cs tests/LearnPlayAI.Core.Tests/Services/SettingsServiceTests.cs tests/LearnPlayAI.Core.Tests/Fakes/FakeSettingsRepository.cs
git commit -m "Add SettingsService with default keyboard shortcut seeding"
```

---

### Task 11: Player abstraction and LibVLCSharp engine

**Files:**
- Create: `src/LearnPlayAI.Core/Player/PlaybackState.cs`
- Create: `src/LearnPlayAI.Core/Player/PlaybackRate.cs`
- Create: `src/LearnPlayAI.Core/Player/IPlayerEngine.cs`
- Create: `src/LearnPlayAI.Core/Player/LibVlcPlayerEngine.cs`
- Test: `tests/LearnPlayAI.Core.Tests/Player/PlaybackRateTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks (uses `LibVLCSharp.Shared` directly).
- Produces: `PlaybackState` enum; `PlaybackRate.Clamp(float) : float`; `IPlayerEngine` (events `PositionChanged`, `StateChanged`, `DurationChanged`, `ErrorOccurred`; properties `State`, `Position`, `Duration`, `Rate`, `AudioTracks`, `SubtitleTracks`; methods `Open`, `Play`, `Pause`, `Stop`, `Seek`, `StepFrame`, `SetRate`, `SetAudioTrack`, `SetSubtitleTrack`, `SetSubtitleDelay`, `SetVideoAdjust`, `Screenshot`); `LibVlcPlayerEngine : IPlayerEngine` with an additional `NativeMediaPlayer : MediaPlayer` property. Consumed by Task 12 (App composition root), Task 14 (`PlayerViewModel`/`PlayerView`), and Task 15–17 (playback feature wiring).

Only `PlaybackRate.Clamp` is independently unit-testable without a real video file and a rendered window; `LibVlcPlayerEngine` itself is verified manually in Task 14's smoke test once wired into the UI, consistent with the design spec's testing approach.

- [ ] **Step 1: Write the failing test for the testable piece**

`tests/LearnPlayAI.Core.Tests/Player/PlaybackRateTests.cs`:

```csharp
using LearnPlayAI.Core.Player;

namespace LearnPlayAI.Core.Tests.Player;

public class PlaybackRateTests
{
    [Theory]
    [InlineData(0.1f, 0.25f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(4.0f, 4.0f)]
    [InlineData(10.0f, 4.0f)]
    public void Clamp_KeepsRateWithinZeroPointTwoFiveToFourX(float requested, float expected)
    {
        Assert.Equal(expected, PlaybackRate.Clamp(requested));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter PlaybackRateTests
```

Expected: FAIL — `PlaybackRate` does not exist yet.

- [ ] **Step 3: Write `PlaybackState` and `PlaybackRate`**

`src/LearnPlayAI.Core/Player/PlaybackState.cs`:

```csharp
namespace LearnPlayAI.Core.Player;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Ended,
    Error
}
```

`src/LearnPlayAI.Core/Player/PlaybackRate.cs`:

```csharp
namespace LearnPlayAI.Core.Player;

public static class PlaybackRate
{
    public const float Minimum = 0.25f;
    public const float Maximum = 4.0f;

    public static float Clamp(float requested) => Math.Clamp(requested, Minimum, Maximum);
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test tests/LearnPlayAI.Core.Tests --filter PlaybackRateTests
```

Expected: PASS (5 cases).

- [ ] **Step 5: Write the `IPlayerEngine` interface**

`src/LearnPlayAI.Core/Player/IPlayerEngine.cs`:

```csharp
namespace LearnPlayAI.Core.Player;

public interface IPlayerEngine : IDisposable
{
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler<TimeSpan>? DurationChanged;
    event EventHandler<string>? ErrorOccurred;

    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    float Rate { get; }

    IReadOnlyList<(int Id, string Name)> AudioTracks { get; }
    IReadOnlyList<(int Id, string Name)> SubtitleTracks { get; }

    void Open(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    void StepFrame();
    void SetRate(float rate);
    void SetAudioTrack(int trackId);
    void SetSubtitleTrack(int trackId);
    void SetSubtitleDelay(TimeSpan delay);
    void SetVideoAdjust(float brightness, float contrast, float saturation);
    string Screenshot(string outputDirectory);
}
```

- [ ] **Step 6: Write the LibVLCSharp-backed implementation**

`src/LearnPlayAI.Core/Player/LibVlcPlayerEngine.cs`:

```csharp
using LibVLCSharp.Shared;

namespace LearnPlayAI.Core.Player;

public sealed class LibVlcPlayerEngine : IPlayerEngine
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? StateChanged;
    public event EventHandler<TimeSpan>? DurationChanged;
    public event EventHandler<string>? ErrorOccurred;

    public PlaybackState State { get; private set; } = PlaybackState.Stopped;
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public float Rate { get; private set; } = 1.0f;
    public IReadOnlyList<(int Id, string Name)> AudioTracks { get; private set; } = Array.Empty<(int, string)>();
    public IReadOnlyList<(int Id, string Name)> SubtitleTracks { get; private set; } = Array.Empty<(int, string)>();

    public MediaPlayer NativeMediaPlayer => _mediaPlayer;

    public LibVlcPlayerEngine()
    {
        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        _mediaPlayer.TimeChanged += (_, e) =>
        {
            Position = TimeSpan.FromMilliseconds(e.Time);
            PositionChanged?.Invoke(this, Position);
        };
        _mediaPlayer.LengthChanged += (_, e) =>
        {
            Duration = TimeSpan.FromMilliseconds(e.Length);
            DurationChanged?.Invoke(this, Duration);
        };
        _mediaPlayer.Playing += (_, _) =>
        {
            RefreshTracks();
            SetState(PlaybackState.Playing);
        };
        _mediaPlayer.Paused += (_, _) => SetState(PlaybackState.Paused);
        _mediaPlayer.Stopped += (_, _) => SetState(PlaybackState.Stopped);
        _mediaPlayer.EndReached += (_, _) => SetState(PlaybackState.Ended);
        _mediaPlayer.EncounteredError += (_, _) =>
        {
            SetState(PlaybackState.Error);
            ErrorOccurred?.Invoke(this, "libVLC encountered a playback error.");
        };
    }

    public void Open(string filePath)
    {
        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVlc, filePath, FromType.FromPath);
        _mediaPlayer.Media = _currentMedia;
    }

    public void Play() => _mediaPlayer.Play();

    public void Pause() => _mediaPlayer.Pause();

    public void Stop() => _mediaPlayer.Stop();

    public void Seek(TimeSpan position) => _mediaPlayer.Time = (long)position.TotalMilliseconds;

    public void StepFrame() => _mediaPlayer.NextFrame();

    public void SetRate(float rate)
    {
        Rate = PlaybackRate.Clamp(rate);
        _mediaPlayer.SetRate(Rate);
    }

    public void SetAudioTrack(int trackId) => _mediaPlayer.SetAudioTrack(trackId);

    public void SetSubtitleTrack(int trackId) => _mediaPlayer.SetSpu(trackId);

    public void SetSubtitleDelay(TimeSpan delay) => _mediaPlayer.SetSpuDelay((long)delay.TotalMicroseconds);

    public void SetVideoAdjust(float brightness, float contrast, float saturation)
    {
        _mediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Brightness, brightness);
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Contrast, contrast);
        _mediaPlayer.SetAdjustFloat(VideoAdjustOption.Saturation, saturation);
    }

    public string Screenshot(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        _mediaPlayer.TakeSnapshot(0, path, 0, 0);
        return path;
    }

    private void RefreshTracks()
    {
        AudioTracks = _mediaPlayer.AudioTrackDescription?
            .Select(t => (t.Id, t.Name)).ToList() ?? new List<(int, string)>();
        SubtitleTracks = _mediaPlayer.SpuDescription?
            .Select(t => (t.Id, t.Name)).ToList() ?? new List<(int, string)>();
    }

    private void SetState(PlaybackState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    public void Dispose()
    {
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
}
```

- [ ] **Step 7: Build to verify it compiles**

```bash
dotnet build src/LearnPlayAI.Core/LearnPlayAI.Core.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/LearnPlayAI.Core/Player tests/LearnPlayAI.Core.Tests/Player
git commit -m "Add player abstraction and LibVLCSharp-backed engine"
```

---

### Task 12: App composition root and minimal startup window

**Files:**
- Modify: `src/LearnPlayAI.App/App.xaml`
- Modify: `src/LearnPlayAI.App/App.xaml.cs`
- Modify: `src/LearnPlayAI.App/MainWindow.xaml`
- Modify: `src/LearnPlayAI.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `IVideoHasher`/`VideoHasher` (Task 3), `IVideoLibraryRepository`/`IPlaylistRepository`/`ISettingsRepository` and their SQLite implementations (Tasks 2, 5–7), `LibraryService`/`PlaylistService`/`SettingsService` (Tasks 8–10), `IPlayerEngine`/`LibVlcPlayerEngine` (Task 11).
- Produces: `App.Services : IServiceProvider`, a static, app-wide service locator every later view/view-model task resolves dependencies from. `MainWindow` is a temporary placeholder window, replaced wholesale by `MainShellWindow` in Task 13 — do not build on `MainWindow`'s internals beyond this task.

This task has no automated test (it's WPF application wiring); it is verified by running the app and confirming it launches without error — consistent with the design spec's decision to manually verify UI/runtime behavior.

- [ ] **Step 1: Write the composition root**

`src/LearnPlayAI.App/App.xaml`:

```xml
<Application x:Class="LearnPlayAI.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources />
</Application>
```

`src/LearnPlayAI.App/App.xaml.cs`:

```csharp
using System.IO;
using System.Windows;
using LearnPlayAI.Core.Hashing;
using LearnPlayAI.Core.Player;
using LearnPlayAI.Core.Repositories;
using LearnPlayAI.Core.Services;
using LearnPlayAI.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LearnPlayAI.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LearnPlayAI", "learnplay.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var services = new ServiceCollection();
        services.AddSingleton<IVideoHasher, VideoHasher>();
        services.AddSingleton<IVideoLibraryRepository>(_ => new SqliteVideoLibraryRepository(dbPath));
        services.AddSingleton<IPlaylistRepository>(_ => new SqlitePlaylistRepository(dbPath));
        services.AddSingleton<ISettingsRepository>(_ => new SqliteSettingsRepository(dbPath));
        services.AddSingleton<LibraryService>();
        services.AddSingleton<PlaylistService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IPlayerEngine, LibVlcPlayerEngine>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.InitializeAsync().GetAwaiter().GetResult();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```

`src/LearnPlayAI.App/MainWindow.xaml`:

```xml
<Window x:Class="LearnPlayAI.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LearnPlay AI" Height="800" Width="1280">
    <Grid>
        <TextBlock x:Name="StatusText" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="20" />
    </Grid>
</Window>
```

`src/LearnPlayAI.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App;

public partial class MainWindow : Window
{
    public MainWindow(SettingsService settingsService)
    {
        InitializeComponent();
        StatusText.Text = $"LearnPlay AI starting up (theme: {settingsService.Current.Theme})";
    }
}
```

- [ ] **Step 2: Build and run to verify it launches**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: a window titled "LearnPlay AI" opens showing "LearnPlay AI starting up (theme: Dark)". Close the window to exit.

- [ ] **Step 3: Commit**

```bash
git add src/LearnPlayAI.App/App.xaml src/LearnPlayAI.App/App.xaml.cs src/LearnPlayAI.App/MainWindow.xaml src/LearnPlayAI.App/MainWindow.xaml.cs
git commit -m "Wire up DI composition root and a minimal startup window"
```

---

### Task 13: ThemeManager, Dark/Light themes, and the full application shell

**Files:**
- Delete: `src/LearnPlayAI.App/MainWindow.xaml`, `src/LearnPlayAI.App/MainWindow.xaml.cs`
- Create: `src/LearnPlayAI.App/Themes/Dark.xaml`
- Create: `src/LearnPlayAI.App/Themes/Light.xaml`
- Create: `src/LearnPlayAI.App/Services/ThemeManager.cs`
- Create: `src/LearnPlayAI.App/Views/MainShellWindow.xaml`
- Create: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`
- Modify: `src/LearnPlayAI.App/App.xaml.cs`

**Interfaces:**
- Consumes: `SettingsService` (Task 10).
- Produces: `ThemeManager.Apply(ThemeMode)`, the application shell (`MainShellWindow`) with left nav (Library/Playlists/Recent/Notes-disabled/Settings), a `CenterContent` `ContentControl`, a right sidebar AI/Notes placeholder, and a `TransportBarHost` `Border` reserved for Task 14. Tasks 14, 17–21 each replace one of the nav placeholder handlers or the `TransportBarHost` with real content — they modify this file rather than recreate it.

- [ ] **Step 1: Remove the temporary MainWindow**

```bash
rm src/LearnPlayAI.App/MainWindow.xaml src/LearnPlayAI.App/MainWindow.xaml.cs
```

- [ ] **Step 2: Write the theme resource dictionaries**

`src/LearnPlayAI.App/Themes/Dark.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="App.Background" Color="#FF1E1E1E" />
    <SolidColorBrush x:Key="Sidebar.Background" Color="#FF252526" />
    <SolidColorBrush x:Key="Text.Primary" Color="#FFE8E8E8" />
    <SolidColorBrush x:Key="Accent" Color="#FF3794FF" />
    <SolidColorBrush x:Key="Border.Default" Color="#FF3C3C3C" />

    <Style x:Key="NavButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource Text.Primary}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="16,12" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="FontSize" Value="14" />
    </Style>
</ResourceDictionary>
```

`src/LearnPlayAI.App/Themes/Light.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="App.Background" Color="#FFF5F5F5" />
    <SolidColorBrush x:Key="Sidebar.Background" Color="#FFE8E8E8" />
    <SolidColorBrush x:Key="Text.Primary" Color="#FF1E1E1E" />
    <SolidColorBrush x:Key="Accent" Color="#FF0A64C4" />
    <SolidColorBrush x:Key="Border.Default" Color="#FFCFCFCF" />

    <Style x:Key="NavButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="Foreground" Value="{StaticResource Text.Primary}" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="16,12" />
        <Setter Property="HorizontalContentAlignment" Value="Left" />
        <Setter Property="FontSize" Value="14" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 3: Write ThemeManager**

`src/LearnPlayAI.App/Services/ThemeManager.cs`:

```csharp
using System.Linq;
using System.Windows;
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.App.Services;

public sealed class ThemeManager
{
    public void Apply(ThemeMode theme)
    {
        var dictUri = theme == ThemeMode.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var newDictionary = new ResourceDictionary { Source = dictUri };
        var app = Application.Current;

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Themes/"));
        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }
        app.Resources.MergedDictionaries.Add(newDictionary);
    }
}
```

- [ ] **Step 4: Write the application shell**

`src/LearnPlayAI.App/Views/MainShellWindow.xaml`:

```xml
<Window x:Class="LearnPlayAI.App.Views.MainShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LearnPlay AI" Height="800" Width="1280"
        Background="{DynamicResource App.Background}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="90" />
        </Grid.RowDefinitions>

        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220" MinWidth="160" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="280" MinWidth="200" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0" Background="{DynamicResource Sidebar.Background}">
                <Button Content="Library" Click="OnNavLibrary" Style="{StaticResource NavButtonStyle}" />
                <Button Content="Playlists" Click="OnNavPlaylists" Style="{StaticResource NavButtonStyle}" />
                <Button Content="Recent" Click="OnNavRecent" Style="{StaticResource NavButtonStyle}" />
                <Button Content="Notes" IsEnabled="False" Style="{StaticResource NavButtonStyle}" />
                <Button Content="Settings" Click="OnNavSettings" Style="{StaticResource NavButtonStyle}" />
            </StackPanel>

            <ContentControl x:Name="CenterContent" Grid.Column="1" />

            <Border Grid.Column="2" Background="{DynamicResource Sidebar.Background}">
                <TextBlock Text="AI Assistant and live notes arrive in a later phase."
                           Foreground="{DynamicResource Text.Primary}"
                           Margin="16" Opacity="0.6" TextWrapping="Wrap" />
            </Border>
        </Grid>

        <Border x:Name="TransportBarHost" Grid.Row="1" Background="{DynamicResource Sidebar.Background}"
                BorderBrush="{DynamicResource Border.Default}" BorderThickness="0,1,0,0" />
    </Grid>
</Window>
```

`src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.Services;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;

    public MainShellWindow(SettingsService settingsService, ThemeManager themeManager)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;

        _themeManager.Apply(_settingsService.Current.Theme);
        CenterContent.Content = BuildPlaceholder("Select Library, Playlists, or Recent from the left to get started.");
    }

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Library panel arrives in a later task.");

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Playlists panel arrives in a later task.");

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Recent panel arrives in a later task.");

    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var themeLabel = new TextBlock
        {
            Text = $"Current theme: {_settingsService.Current.Theme}",
            Foreground = (Brush)FindResource("Text.Primary"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var toggleButton = new Button
        {
            Content = "Toggle theme",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toggleButton.Click += async (_, _) =>
        {
            var next = _settingsService.Current.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            await _settingsService.SetThemeAsync(next);
            _themeManager.Apply(next);
            themeLabel.Text = $"Current theme: {next}";
        };
        panel.Children.Add(themeLabel);
        panel.Children.Add(toggleButton);
        CenterContent.Content = panel;
    }

    private UIElement BuildPlaceholder(string text) => new TextBlock
    {
        Text = text,
        Foreground = (Brush)FindResource("Text.Primary"),
        Margin = new Thickness(24),
        TextWrapping = TextWrapping.Wrap
    };
}
```

- [ ] **Step 5: Wire `MainShellWindow` and `ThemeManager` into the composition root**

In `src/LearnPlayAI.App/App.xaml.cs`, replace:

```csharp
        services.AddSingleton<IPlayerEngine, LibVlcPlayerEngine>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.InitializeAsync().GetAwaiter().GetResult();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
```

with:

```csharp
        services.AddSingleton<IPlayerEngine, LibVlcPlayerEngine>();
        services.AddSingleton<Services.ThemeManager>();
        services.AddSingleton<Views.MainShellWindow>();

        Services = services.BuildServiceProvider();

        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.InitializeAsync().GetAwaiter().GetResult();

        var mainWindow = Services.GetRequiredService<Views.MainShellWindow>();
        mainWindow.Show();
```

- [ ] **Step 6: Build and run to verify the shell and theme toggle work**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: the shell opens with the left nav, empty center pane, right "AI Assistant" placeholder, and a thin bottom bar. Click each left nav button and confirm the center pane text changes. Click **Settings**, then **Toggle theme**, and confirm the whole window's background/text colors swap between dark and light. Close and relaunch the app; confirm the theme you left it on is still applied (proves persistence through `SettingsService`).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add ThemeManager, Dark/Light themes, and the full application shell"
```

---

### Task 14: Core playback — PlayerView, TransportBarView, PlayerViewModel

**Files:**
- Modify: `src/LearnPlayAI.Core/Player/IPlayerEngine.cs` (add `Volume`)
- Modify: `src/LearnPlayAI.Core/Player/LibVlcPlayerEngine.cs` (implement `Volume`)
- Create: `src/LearnPlayAI.App/Converters/SecondsToTimeStringConverter.cs`
- Create: `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`
- Create: `src/LearnPlayAI.App/Views/PlayerView.xaml`
- Create: `src/LearnPlayAI.App/Views/PlayerView.xaml.cs`
- Create: `src/LearnPlayAI.App/Views/TransportBarView.xaml`
- Create: `src/LearnPlayAI.App/Views/TransportBarView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`
- Modify: `src/LearnPlayAI.App/App.xaml.cs`

**Interfaces:**
- Consumes: `IPlayerEngine`/`LibVlcPlayerEngine` (Task 11), `LibraryService`/`SettingsService` (Tasks 8, 10), `MainShellWindow` (Task 13).
- Produces: `PlayerViewModel` with `PositionSeconds`, `DurationSeconds`, `CurrentTitle`, `IsPlaying`, `Volume` (bindable properties), `PlayPauseCommand`, `StopCommand`, `SeekCommand(double seconds)`, and `LoadVideo(VideoEntry)`. `PlayerView.PlayVideo(VideoEntry)`. Tasks 15–16 extend `PlayerViewModel`/`TransportBarView` with speed/frame-step/screenshot/fullscreen/tracks/adjustments. Tasks 17–19 call `MainShellWindow`'s video-opening path (exposed here as `OpenVideo(VideoEntry)`) from the Library/Recent/Playlists panels.

This task is verified manually (real video playback/rendering), consistent with the design spec.

- [ ] **Step 1: Add `Volume` to the player engine**

In `src/LearnPlayAI.Core/Player/IPlayerEngine.cs`, replace:

```csharp
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    float Rate { get; }

    IReadOnlyList<(int Id, string Name)> AudioTracks { get; }
```

with:

```csharp
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    float Rate { get; }
    float Volume { get; set; }

    IReadOnlyList<(int Id, string Name)> AudioTracks { get; }
```

In `src/LearnPlayAI.Core/Player/LibVlcPlayerEngine.cs`, replace:

```csharp
    public float Rate { get; private set; } = 1.0f;
    public IReadOnlyList<(int Id, string Name)> AudioTracks { get; private set; } = Array.Empty<(int, string)>();
```

with:

```csharp
    public float Rate { get; private set; } = 1.0f;

    public float Volume
    {
        get => _mediaPlayer.Volume / 100f;
        set => _mediaPlayer.Volume = (int)Math.Clamp(value * 100f, 0f, 100f);
    }

    public IReadOnlyList<(int Id, string Name)> AudioTracks { get; private set; } = Array.Empty<(int, string)>();
```

- [ ] **Step 2: Write the time-display converter**

`src/LearnPlayAI.App/Converters/SecondsToTimeStringConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace LearnPlayAI.App.Converters;

public sealed class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var seconds = value is double d ? d : 0d;
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 3: Write PlayerViewModel**

`src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`:

```csharp
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public partial class PlayerViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private VideoEntry? _currentEntry;
    private DateTime _lastPersistUtc = DateTime.MinValue;

    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private double positionSeconds;
    [ObservableProperty] private double durationSeconds;
    [ObservableProperty] private string currentTitle = "No video loaded";
    [ObservableProperty] private double volume;

    public IPlayerEngine Engine { get; }

    public PlayerViewModel(IPlayerEngine engine, LibraryService libraryService, SettingsService settingsService)
    {
        Engine = engine;
        _libraryService = libraryService;
        volume = settingsService.Current.DefaultVolume;
        Engine.Volume = (float)volume;

        Engine.PositionChanged += (_, pos) =>
        {
            Application.Current.Dispatcher.Invoke(() => PositionSeconds = pos.TotalSeconds);
            MaybePersistPosition(pos);
        };
        Engine.DurationChanged += (_, dur) =>
        {
            Application.Current.Dispatcher.Invoke(() => DurationSeconds = dur.TotalSeconds);
            if (_currentEntry is not null)
            {
                _ = _libraryService.UpdateDurationAsync(_currentEntry.VideoId, dur);
            }
        };
        Engine.StateChanged += (_, state) =>
            Application.Current.Dispatcher.Invoke(() => IsPlaying = state == PlaybackState.Playing);
    }

    public void LoadVideo(VideoEntry entry)
    {
        _currentEntry = entry;
        CurrentTitle = entry.Title;
        Engine.Open(entry.FilePath);
        Engine.Play();
        if (entry.LastPosition > TimeSpan.Zero)
        {
            Engine.Seek(entry.LastPosition);
        }
    }

    private void MaybePersistPosition(TimeSpan position)
    {
        if (_currentEntry is null) return;
        var now = DateTime.UtcNow;
        if ((now - _lastPersistUtc).TotalSeconds < 5) return;
        _lastPersistUtc = now;
        _ = _libraryService.UpdatePlaybackPositionAsync(_currentEntry.VideoId, position);
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (Engine.State == PlaybackState.Playing) Engine.Pause();
        else Engine.Play();
    }

    [RelayCommand]
    private void Stop() => Engine.Stop();

    [RelayCommand]
    private void Seek(double seconds) => Engine.Seek(TimeSpan.FromSeconds(seconds));

    partial void OnVolumeChanged(double value) => Engine.Volume = (float)value;
}
```

- [ ] **Step 4: Write PlayerView**

`src/LearnPlayAI.App/Views/PlayerView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.PlayerView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vlc="clr-namespace:LibVLCSharp.WPF;assembly=LibVLCSharp.WPF">
    <Grid Background="Black">
        <vlc:VideoView x:Name="VideoViewControl" />
    </Grid>
</UserControl>
```

`src/LearnPlayAI.App/Views/PlayerView.xaml.cs`:

```csharp
using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;

namespace LearnPlayAI.App.Views;

public partial class PlayerView : UserControl
{
    private readonly PlayerViewModel _viewModel;

    public PlayerView(PlayerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        if (_viewModel.Engine is LibVlcPlayerEngine libVlc)
        {
            VideoViewControl.MediaPlayer = libVlc.NativeMediaPlayer;
        }
    }

    public void PlayVideo(VideoEntry entry) => _viewModel.LoadVideo(entry);
}
```

- [ ] **Step 5: Write TransportBarView**

`src/LearnPlayAI.App/Views/TransportBarView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.TransportBarView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:converters="clr-namespace:LearnPlayAI.App.Converters">
    <UserControl.Resources>
        <converters:SecondsToTimeStringConverter x:Key="SecondsToTimeStringConverter" />
    </UserControl.Resources>
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <Slider x:Name="ScrubberSlider" Grid.Row="0"
                Minimum="0" Maximum="{Binding DurationSeconds}"
                Value="{Binding PositionSeconds, Mode=OneWay}"
                IsMoveToPointEnabled="True"
                PreviewMouseUp="OnScrubberPreviewMouseUp" />

        <StackPanel Grid.Row="1" Orientation="Horizontal" VerticalAlignment="Center">
            <Button Content="Stop" Command="{Binding StopCommand}" Margin="0,0,8,0" />
            <Button Content="Play / Pause" Command="{Binding PlayPauseCommand}" Margin="0,0,16,0" />
            <TextBlock Text="{Binding PositionSeconds, Converter={StaticResource SecondsToTimeStringConverter}}"
                       Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" />
            <TextBlock Text=" / " Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" />
            <TextBlock Text="{Binding DurationSeconds, Converter={StaticResource SecondsToTimeStringConverter}}"
                       Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,16,0" />
            <TextBlock Text="Volume" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,8,0" />
            <Slider Width="120" Minimum="0" Maximum="1" Value="{Binding Volume}" VerticalAlignment="Center" />
        </StackPanel>
    </Grid>
</UserControl>
```

`src/LearnPlayAI.App/Views/TransportBarView.xaml.cs`:

```csharp
using System.Windows.Controls;
using System.Windows.Input;
using LearnPlayAI.App.ViewModels;

namespace LearnPlayAI.App.Views;

public partial class TransportBarView : UserControl
{
    public TransportBarView(PlayerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnScrubberPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.SeekCommand.Execute(ScrubberSlider.Value);
        }
    }
}
```

- [ ] **Step 6: Wire the player into the shell**

Replace the entire contents of `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs` with:

```csharp
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlayerView _playerView;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;

        _themeManager.Apply(_settingsService.Current.Theme);

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        CenterContent.Content = BuildHomePlaceholder();
    }

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Library panel arrives in a later task.");

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Playlists panel arrives in a later task.");

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Recent panel arrives in a later task.");

    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var themeLabel = new TextBlock
        {
            Text = $"Current theme: {_settingsService.Current.Theme}",
            Foreground = (Brush)FindResource("Text.Primary"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var toggleButton = new Button
        {
            Content = "Toggle theme",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toggleButton.Click += async (_, _) =>
        {
            var next = _settingsService.Current.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            await _settingsService.SetThemeAsync(next);
            _themeManager.Apply(next);
            themeLabel.Text = $"Current theme: {next}";
        };
        panel.Children.Add(themeLabel);
        panel.Children.Add(toggleButton);
        CenterContent.Content = panel;
    }

    private UIElement BuildHomePlaceholder()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select Library, Playlists, or Recent from the left, or open a file directly:",
            Foreground = (Brush)FindResource("Text.Primary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var openButton = new Button
        {
            Content = "Open Video File...",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Click += async (_, _) => await OpenFileDialogAsync();
        panel.Children.Add(openButton);
        return panel;
    }

    private async Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var entry = await _libraryService.RegisterFileAsync(dialog.FileName);
        OpenVideo(entry);
    }

    private void OpenVideo(VideoEntry entry)
    {
        CenterContent.Content = _playerView;
        _playerView.PlayVideo(entry);
    }

    private UIElement BuildPlaceholder(string text) => new TextBlock
    {
        Text = text,
        Foreground = (Brush)FindResource("Text.Primary"),
        Margin = new Thickness(24),
        TextWrapping = TextWrapping.Wrap
    };
}
```

- [ ] **Step 7: Register the new types in the composition root**

In `src/LearnPlayAI.App/App.xaml.cs`, replace:

```csharp
        services.AddSingleton<IPlayerEngine, LibVlcPlayerEngine>();
        services.AddSingleton<Services.ThemeManager>();
        services.AddSingleton<Views.MainShellWindow>();
```

with:

```csharp
        services.AddSingleton<IPlayerEngine, LibVlcPlayerEngine>();
        services.AddSingleton<Services.ThemeManager>();
        services.AddSingleton<ViewModels.PlayerViewModel>();
        services.AddSingleton<Views.TransportBarView>();
        services.AddSingleton<Views.MainShellWindow>();
```

Add `using LearnPlayAI.App.ViewModels;` is not required since the registration above uses the fully-qualified `ViewModels.PlayerViewModel`; keep the existing `using LearnPlayAI.Core...` lines as they are.

- [ ] **Step 8: Build and manually verify playback**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: click **Open Video File...**, pick a local MP4/MKV file, and confirm: video renders in the center pane; Play/Pause toggles playback; Stop halts playback; the scrubber advances during playback and seeking by dragging it jumps to that position; the volume slider changes audio level; the time display shows correct elapsed/total time.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Add core playback: PlayerView, TransportBarView, PlayerViewModel"
```

---

### Task 15: Speed control, frame-step, screenshot, full-screen

**Files:**
- Modify: `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`
- Modify: `src/LearnPlayAI.App/Views/TransportBarView.xaml`
- Modify: `src/LearnPlayAI.App/Views/TransportBarView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `PlaybackRate` (Task 11), `IPlayerEngine.StepFrame/SetRate/Screenshot` (Task 11), `PlayerViewModel` (Task 14).
- Produces: `PlayerViewModel.Rate`, `SpeedUpCommand`, `SpeedDownCommand`, `StepFrameCommand`, `ScreenshotCommand`; `MainShellWindow.ToggleFullScreen()`. Task 20 (`KeyboardShortcutManager`) binds these commands to the default shortcuts defined in Task 10 (`SpeedUp`, `SpeedDown`, `StepFrame`, `Screenshot`, `ToggleFullScreen`).

Verified manually, same rationale as Task 14.

- [ ] **Step 1: Extend PlayerViewModel**

In `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`, add a `Rate` property and the four new commands. Replace:

```csharp
    [ObservableProperty] private string currentTitle = "No video loaded";
    [ObservableProperty] private double volume;
```

with:

```csharp
    [ObservableProperty] private string currentTitle = "No video loaded";
    [ObservableProperty] private double volume;
    [ObservableProperty] private float rate = 1.0f;
```

Then, immediately after the existing `Seek` command method, add:

```csharp
    [RelayCommand]
    private void StepFrame() => Engine.StepFrame();

    [RelayCommand]
    private void SpeedUp() => SetRate(Rate + 0.25f);

    [RelayCommand]
    private void SpeedDown() => SetRate(Rate - 0.25f);

    [RelayCommand]
    private void Screenshot()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LearnPlayAI");
        Engine.Screenshot(dir);
    }

    private void SetRate(float requested)
    {
        Rate = PlaybackRate.Clamp(requested);
        Engine.SetRate(Rate);
    }
```

Add `using System.IO;` to the top of the file alongside the existing `using` statements.

- [ ] **Step 2: Extend TransportBarView with speed and screenshot controls**

In `src/LearnPlayAI.App/Views/TransportBarView.xaml`, replace:

```xml
            <Button Content="Stop" Command="{Binding StopCommand}" Margin="0,0,8,0" />
            <Button Content="Play / Pause" Command="{Binding PlayPauseCommand}" Margin="0,0,16,0" />
```

with:

```xml
            <Button Content="Stop" Command="{Binding StopCommand}" Margin="0,0,8,0" />
            <Button Content="Play / Pause" Command="{Binding PlayPauseCommand}" Margin="0,0,8,0" />
            <Button Content="Frame &gt;" Command="{Binding StepFrameCommand}" Margin="0,0,8,0" ToolTip="Step one frame forward" />
            <Button Content="-" Command="{Binding SpeedDownCommand}" Margin="0,0,0,0" ToolTip="Slower" />
            <TextBlock Text="{Binding Rate, StringFormat={}{0:0.00}x}" Foreground="{DynamicResource Text.Primary}"
                       VerticalAlignment="Center" Margin="4,0" Width="40" TextAlignment="Center" />
            <Button Content="+" Command="{Binding SpeedUpCommand}" Margin="0,0,16,0" ToolTip="Faster" />
            <Button Content="Screenshot" Command="{Binding ScreenshotCommand}" Margin="0,0,16,0" />
```

- [ ] **Step 3: Add full-screen toggle to the shell**

In `src/LearnPlayAI.App/Views/TransportBarView.xaml`, add a full-screen button right after the Screenshot button added above:

```xml
            <Button x:Name="FullScreenButton" Content="Full Screen" Click="OnFullScreenClick" Margin="0,0,16,0" />
```

In `src/LearnPlayAI.App/Views/TransportBarView.xaml.cs`, replace the whole file with:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LearnPlayAI.App.ViewModels;

namespace LearnPlayAI.App.Views;

public partial class TransportBarView : UserControl
{
    public TransportBarView(PlayerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnScrubberPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.SeekCommand.Execute(ScrubberSlider.Value);
        }
    }

    private void OnFullScreenClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Views.MainShellWindow shell)
        {
            shell.ToggleFullScreen();
        }
    }
}
```

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`, add this public method (anywhere inside the `MainShellWindow` class body, e.g. right after the constructor):

```csharp
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }
```

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected, with a video open: **+**/**-** change the displayed rate in 0.25 steps and clamp at 0.25x/4x; the **Frame >** button advances exactly one frame while paused; **Screenshot** writes a PNG to `Pictures\LearnPlayAI\`; **Full Screen** removes the window chrome and maximizes, and clicking it again restores the previous window state.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add speed control, frame-stepping, screenshot capture, and full-screen toggle"
```

---

### Task 16: Audio/subtitle track selection, subtitle sync, video adjustments

**Files:**
- Modify: `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`
- Modify: `src/LearnPlayAI.App/Views/TransportBarView.xaml`

**Interfaces:**
- Consumes: `IPlayerEngine.AudioTracks/SubtitleTracks/SetAudioTrack/SetSubtitleTrack/SetSubtitleDelay/SetVideoAdjust` (Task 11), `PlayerViewModel` (Task 14).
- Produces: `PlayerViewModel.AudioTracks`, `SubtitleTracks`, `SelectAudioTrackCommand`, `SelectSubtitleTrackCommand`, `SubtitleDelaySeconds`, `Brightness`/`Contrast`/`Saturation` bindable properties. No later task depends on these beyond this one; they are leaf UI features.

Verified manually (requires a real video with multiple tracks to fully exercise, but behaves correctly with a single-track file too — the track menu simply shows one entry).

- [ ] **Step 1: Extend PlayerViewModel with track selection, subtitle sync, and video adjustments**

In `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`, replace:

```csharp
    [ObservableProperty] private float rate = 1.0f;
```

with:

```csharp
    [ObservableProperty] private float rate = 1.0f;
    [ObservableProperty] private IReadOnlyList<(int Id, string Name)> audioTracks = Array.Empty<(int, string)>();
    [ObservableProperty] private IReadOnlyList<(int Id, string Name)> subtitleTracks = Array.Empty<(int, string)>();
    [ObservableProperty] private double subtitleDelaySeconds;
    [ObservableProperty] private float brightness = 1.0f;
    [ObservableProperty] private float contrast = 1.0f;
    [ObservableProperty] private float saturation = 1.0f;
```

Replace the `Engine.StateChanged += ...` subscription:

```csharp
        Engine.StateChanged += (_, state) =>
            Application.Current.Dispatcher.Invoke(() => IsPlaying = state == PlaybackState.Playing);
```

with:

```csharp
        Engine.StateChanged += (_, state) => Application.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            if (state == PlaybackState.Playing)
            {
                AudioTracks = Engine.AudioTracks;
                SubtitleTracks = Engine.SubtitleTracks;
            }
        });
```

Add these commands and property-changed hooks after the existing `SetRate` private method:

```csharp
    [RelayCommand]
    private void SelectAudioTrack(int trackId) => Engine.SetAudioTrack(trackId);

    [RelayCommand]
    private void SelectSubtitleTrack(int trackId) => Engine.SetSubtitleTrack(trackId);

    partial void OnSubtitleDelaySecondsChanged(double value) =>
        Engine.SetSubtitleDelay(TimeSpan.FromSeconds(value));

    partial void OnBrightnessChanged(float value) => ApplyVideoAdjust();
    partial void OnContrastChanged(float value) => ApplyVideoAdjust();
    partial void OnSaturationChanged(float value) => ApplyVideoAdjust();

    private void ApplyVideoAdjust() => Engine.SetVideoAdjust(Brightness, Contrast, Saturation);
```

- [ ] **Step 2: Add track menus and adjustment sliders to TransportBarView**

In `src/LearnPlayAI.App/Views/TransportBarView.xaml`, replace the closing of the horizontal `StackPanel` (everything from the Volume slider to `</StackPanel>`):

```xml
            <TextBlock Text="Volume" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,8,0" />
            <Slider Width="120" Minimum="0" Maximum="1" Value="{Binding Volume}" VerticalAlignment="Center" />
        </StackPanel>
```

with:

```xml
            <TextBlock Text="Volume" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,8,0" />
            <Slider Width="100" Minimum="0" Maximum="1" Value="{Binding Volume}" VerticalAlignment="Center" Margin="0,0,16,0" />

            <TextBlock Text="Audio" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,4,0" />
            <ComboBox Width="110" ItemsSource="{Binding AudioTracks}" DisplayMemberPath="Name" Margin="0,0,16,0"
                      SelectionChanged="OnAudioTrackSelectionChanged" />

            <TextBlock Text="Subtitles" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,4,0" />
            <ComboBox Width="110" ItemsSource="{Binding SubtitleTracks}" DisplayMemberPath="Name" Margin="0,0,16,0"
                      SelectionChanged="OnSubtitleTrackSelectionChanged" />

            <TextBlock Text="Sub sync (s)" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,4,0" />
            <Slider Width="80" Minimum="-10" Maximum="10" Value="{Binding SubtitleDelaySeconds}" VerticalAlignment="Center" Margin="0,0,16,0" />

            <TextBlock Text="B" ToolTip="Brightness" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,2,0" />
            <Slider Width="60" Minimum="0" Maximum="2" Value="{Binding Brightness}" VerticalAlignment="Center" Margin="0,0,8,0" />
            <TextBlock Text="C" ToolTip="Contrast" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,2,0" />
            <Slider Width="60" Minimum="0" Maximum="2" Value="{Binding Contrast}" VerticalAlignment="Center" Margin="0,0,8,0" />
            <TextBlock Text="S" ToolTip="Saturation" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,2,0" />
            <Slider Width="60" Minimum="0" Maximum="2" Value="{Binding Saturation}" VerticalAlignment="Center" />
        </StackPanel>
```

In `src/LearnPlayAI.App/Views/TransportBarView.xaml.cs`, add these two event handlers inside the `TransportBarView` class (after `OnFullScreenClick`):

```csharp
    private void OnAudioTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ValueTuple<int, string> track } && DataContext is PlayerViewModel viewModel)
        {
            viewModel.SelectAudioTrackCommand.Execute(track.Item1);
        }
    }

    private void OnSubtitleTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ValueTuple<int, string> track } && DataContext is PlayerViewModel viewModel)
        {
            viewModel.SelectSubtitleTrackCommand.Execute(track.Item1);
        }
    }
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected, with a video that has multiple audio/subtitle tracks (or at least embedded subtitles): the Audio and Subtitles combo boxes populate once playback starts and switching selection changes what's audible/displayed; the subtitle sync slider shifts subtitle timing forward/back; the B/C/S sliders visibly change the picture's brightness/contrast/saturation in real time.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Add audio/subtitle track selection, subtitle sync, and video adjustments"
```

---

### Task 17: Drag-and-drop and the Library panel

**Files:**
- Create: `src/LearnPlayAI.App/ViewModels/LibraryViewModel.cs`
- Create: `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml`
- Create: `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `LibraryService` (Task 8), `VideoEntry` (Task 2), `MainShellWindow.OpenVideo(VideoEntry)` (Task 14).
- Produces: `LibraryViewModel(LibraryService, Action<VideoEntry> onOpen)` with `Items`, `AddFilesCommand`, `OpenCommand`, `RemoveCommand`; `LibraryPanelView`. Task 19 extends `LibraryViewModel` with playlist support.

Verified manually (file dialogs and drag-and-drop aren't practically unit-testable).

- [ ] **Step 1: Write LibraryViewModel**

`src/LearnPlayAI.App/ViewModels/LibraryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly Action<VideoEntry> _onOpen;

    public ObservableCollection<VideoEntry> Items { get; } = new();

    public LibraryViewModel(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        _libraryService = libraryService;
        _onOpen = onOpen;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in await _libraryService.GetLibraryAsync())
        {
            Items.Add(entry);
        }
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
        {
            await _libraryService.RegisterFileAsync(file);
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private void Open(VideoEntry entry) => _onOpen(entry);

    [RelayCommand]
    private async Task Remove(VideoEntry entry)
    {
        await _libraryService.RemoveAsync(entry.VideoId);
        await RefreshAsync();
    }
}
```

- [ ] **Step 2: Write LibraryPanelView**

`src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.Panels.LibraryPanelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <Button Grid.Row="0" Content="Add Files..." Command="{Binding AddFilesCommand}"
                HorizontalAlignment="Left" Padding="12,6" Margin="0,0,0,12" />

        <ListBox Grid.Row="1" ItemsSource="{Binding Items}" Background="Transparent" BorderThickness="0">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding Title}" Foreground="{DynamicResource Text.Primary}"
                                   VerticalAlignment="Center" />
                        <Button Grid.Column="1" Content="Play"
                                Command="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Margin="8,0" Padding="8,2" />
                        <Button Grid.Column="2" Content="Remove"
                                Command="{Binding DataContext.RemoveCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Padding="8,2" />
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </Grid>
</UserControl>
```

`src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml.cs`:

```csharp
using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class LibraryPanelView : UserControl
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPanelView(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new LibraryViewModel(libraryService, onOpen);
        DataContext = ViewModel;
    }
}
```

- [ ] **Step 3: Wire the Library nav button and whole-window drag-and-drop**

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml`, replace the `Window` opening tag:

```xml
<Window x:Class="LearnPlayAI.App.Views.MainShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LearnPlay AI" Height="800" Width="1280"
        Background="{DynamicResource App.Background}">
```

with:

```xml
<Window x:Class="LearnPlayAI.App.Views.MainShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LearnPlay AI" Height="800" Width="1280"
        Background="{DynamicResource App.Background}"
        AllowDrop="True" Drop="OnWindowDrop">
```

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`, replace:

```csharp
    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Library panel arrives in a later task.");
```

with:

```csharp
    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.LibraryPanelView(_libraryService, OpenVideo);
```

and add this handler (e.g. right after `OpenFileDialogAsync`):

```csharp
    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        VideoEntry? lastRegistered = null;
        foreach (var file in files)
        {
            lastRegistered = await _libraryService.RegisterFileAsync(file);
        }
        if (lastRegistered is not null)
        {
            OpenVideo(lastRegistered);
        }
    }
```

Add `using LearnPlayAI.App.Views.Panels;` is not required since the call above uses the fully-qualified `Panels.LibraryPanelView`.

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: drag a video file from Explorer onto the window — it registers and starts playing. Click **Library** in the left nav — the dropped file appears in the list. Click **Add Files...**, pick more videos, and confirm they appear. Click **Play** on a library row to open it; click **Remove** to delete it from the library (it disappears from the list immediately).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add drag-and-drop and the Library panel"
```

---

### Task 18: Recent panel and resume-from-last-position

**Files:**
- Create: `src/LearnPlayAI.App/Converters/TimeSpanToShortStringConverter.cs`
- Create: `src/LearnPlayAI.App/ViewModels/RecentViewModel.cs`
- Create: `src/LearnPlayAI.App/Views/Panels/RecentPanelView.xaml`
- Create: `src/LearnPlayAI.App/Views/Panels/RecentPanelView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `LibraryService.GetRecentAsync` (Task 8), `MainShellWindow.OpenVideo(VideoEntry)` (Task 14, which already resumes from `VideoEntry.LastPosition` — no new resume logic needed here, only the UI to reach it).
- Produces: `RecentViewModel(LibraryService, Action<VideoEntry> onOpen)`, `RecentPanelView`. No later task depends on these.

Verified manually.

- [ ] **Step 1: Write the short time converter**

`src/LearnPlayAI.App/Converters/TimeSpanToShortStringConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace LearnPlayAI.App.Converters;

public sealed class TimeSpanToShortStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var span = value is TimeSpan ts ? ts : TimeSpan.Zero;
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: Write RecentViewModel**

`src/LearnPlayAI.App/ViewModels/RecentViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public partial class RecentViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly Action<VideoEntry> _onOpen;

    public ObservableCollection<VideoEntry> Items { get; } = new();

    public RecentViewModel(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        _libraryService = libraryService;
        _onOpen = onOpen;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in await _libraryService.GetRecentAsync())
        {
            Items.Add(entry);
        }
    }

    [RelayCommand]
    private void Open(VideoEntry entry) => _onOpen(entry);
}
```

- [ ] **Step 3: Write RecentPanelView**

`src/LearnPlayAI.App/Views/Panels/RecentPanelView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.Panels.RecentPanelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:converters="clr-namespace:LearnPlayAI.App.Converters">
    <UserControl.Resources>
        <converters:TimeSpanToShortStringConverter x:Key="TimeSpanToShortStringConverter" />
    </UserControl.Resources>
    <Grid Margin="16">
        <ListBox ItemsSource="{Binding Items}" Background="Transparent" BorderThickness="0">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0">
                            <TextBlock Text="{Binding Title}" Foreground="{DynamicResource Text.Primary}" FontWeight="SemiBold" />
                            <TextBlock Foreground="{DynamicResource Text.Primary}" Opacity="0.7">
                                <Run Text="Resume at " /><Run Text="{Binding LastPosition, Converter={StaticResource TimeSpanToShortStringConverter}, Mode=OneWay}" />
                            </TextBlock>
                        </StackPanel>
                        <Button Grid.Column="1"
                                Command="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Content="Resume" Padding="8,2" VerticalAlignment="Center" />
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </Grid>
</UserControl>
```

`src/LearnPlayAI.App/Views/Panels/RecentPanelView.xaml.cs`:

```csharp
using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class RecentPanelView : UserControl
{
    public RecentViewModel ViewModel { get; }

    public RecentPanelView(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new RecentViewModel(libraryService, onOpen);
        DataContext = ViewModel;
    }
}
```

- [ ] **Step 4: Wire the Recent nav button**

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`, replace:

```csharp
    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = BuildPlaceholder("Recent panel arrives in a later task.");
```

with:

```csharp
    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.RecentPanelView(_libraryService, OpenVideo);
```

- [ ] **Step 5: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: open a video, play forward a bit, then click **Stop** (or close and relaunch the app). Click **Recent** — the video appears with a "Resume at m:ss" label close to where you left off. Click **Resume** — playback starts and seeks to that position.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add Recent panel; resume-from-last-position reachable from the UI"
```

---

### Task 19: Playlists panel with auto-advance and skip-on-error

**Files:**
- Modify: `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs` (expose `CurrentVideoId`)
- Modify: `src/LearnPlayAI.App/ViewModels/LibraryViewModel.cs` (add "add to playlist")
- Modify: `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml`
- Modify: `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml.cs`
- Create: `src/LearnPlayAI.App/ViewModels/PlaylistsViewModel.cs`
- Create: `src/LearnPlayAI.App/Views/Panels/PlaylistsPanelView.xaml`
- Create: `src/LearnPlayAI.App/Views/Panels/PlaylistsPanelView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `PlaylistService` (Task 9), `LibraryService` (Task 8), `PlayerViewModel.Engine` events (Task 11/14), `MainShellWindow.OpenVideoEntry` (new in this task, replacing the old `OpenVideo`-only path).
- Produces: `PlaylistsViewModel`, `PlaylistDisplay`/`PlaylistItemDisplay`, `PlaylistsPanelView`; `LibraryViewModel.Playlists`/`AddToPlaylistCommand`; `MainShellWindow.PlayFromPlaylist(Guid playlistId, Guid startVideoId)` and automatic advance-to-next-item on both natural end-of-video and playback error. No later task depends on these beyond this one.

Verified manually (requires at least two videos in a playlist to observe auto-advance).

- [ ] **Step 1: Expose the currently-loaded video id from PlayerViewModel**

In `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`, replace:

```csharp
    public IPlayerEngine Engine { get; }
```

with:

```csharp
    public IPlayerEngine Engine { get; }
    public Guid? CurrentVideoId => _currentEntry?.VideoId;
```

- [ ] **Step 2: Add "add to playlist" support to LibraryViewModel**

Replace the entire contents of `src/LearnPlayAI.App/ViewModels/LibraryViewModel.cs` with:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly Action<VideoEntry> _onOpen;

    public ObservableCollection<VideoEntry> Items { get; } = new();
    public ObservableCollection<Playlist> Playlists { get; } = new();

    public LibraryViewModel(LibraryService libraryService, PlaylistService playlistService, Action<VideoEntry> onOpen)
    {
        _libraryService = libraryService;
        _playlistService = playlistService;
        _onOpen = onOpen;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in await _libraryService.GetLibraryAsync())
        {
            Items.Add(entry);
        }

        Playlists.Clear();
        foreach (var playlist in await _playlistService.GetPlaylistsAsync())
        {
            Playlists.Add(playlist);
        }
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
        {
            await _libraryService.RegisterFileAsync(file);
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private void Open(VideoEntry entry) => _onOpen(entry);

    [RelayCommand]
    private async Task Remove(VideoEntry entry)
    {
        await _libraryService.RemoveAsync(entry.VideoId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddToPlaylist((VideoEntry Video, Playlist Playlist) args) =>
        await _playlistService.AddVideoAsync(args.Playlist.PlaylistId, args.Video.VideoId);
}
```

- [ ] **Step 3: Add a playlist picker to each Library row**

In `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml`, replace:

```xml
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding Title}" Foreground="{DynamicResource Text.Primary}"
                                   VerticalAlignment="Center" />
                        <Button Grid.Column="1" Content="Play"
                                Command="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Margin="8,0" Padding="8,2" />
                        <Button Grid.Column="2" Content="Remove"
                                Command="{Binding DataContext.RemoveCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Padding="8,2" />
```

with:

```xml
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="150" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding Title}" Foreground="{DynamicResource Text.Primary}"
                                   VerticalAlignment="Center" />
                        <Button Grid.Column="1" Content="Play"
                                Command="{Binding DataContext.OpenCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Margin="8,0" Padding="8,2" />
                        <Button Grid.Column="2" Content="Remove"
                                Command="{Binding DataContext.RemoveCommand, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                CommandParameter="{Binding}" Padding="8,2" />
                        <ComboBox Grid.Column="3" Margin="8,0,0,0"
                                  ItemsSource="{Binding DataContext.Playlists, RelativeSource={RelativeSource AncestorType=ListBox}}"
                                  DisplayMemberPath="Name" Tag="{Binding}"
                                  SelectionChanged="OnAddToPlaylistSelectionChanged"
                                  ToolTip="Add to playlist" />
```

In `src/LearnPlayAI.App/Views/Panels/LibraryPanelView.xaml.cs`, replace the entire file with:

```csharp
using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class LibraryPanelView : UserControl
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPanelView(LibraryService libraryService, PlaylistService playlistService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new LibraryViewModel(libraryService, playlistService, onOpen);
        DataContext = ViewModel;
    }

    private void OnAddToPlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: Playlist playlist, Tag: VideoEntry video } comboBox)
        {
            ViewModel.AddToPlaylistCommand.Execute((video, playlist));
            comboBox.SelectedItem = null;
        }
    }
}
```

- [ ] **Step 4: Write PlaylistsViewModel**

`src/LearnPlayAI.App/ViewModels/PlaylistsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public sealed class PlaylistItemDisplay
{
    public required Guid VideoId { get; init; }
    public required string Title { get; init; }
}

public sealed class PlaylistDisplay
{
    public required Guid PlaylistId { get; init; }
    public required string Name { get; init; }
    public required List<PlaylistItemDisplay> Items { get; init; }
}

public partial class PlaylistsViewModel : ObservableObject
{
    private readonly PlaylistService _playlistService;
    private readonly LibraryService _libraryService;
    private readonly Action<Guid, Guid> _onPlay;

    public ObservableCollection<PlaylistDisplay> Playlists { get; } = new();

    [ObservableProperty] private string newPlaylistName = string.Empty;

    public PlaylistsViewModel(PlaylistService playlistService, LibraryService libraryService, Action<Guid, Guid> onPlay)
    {
        _playlistService = playlistService;
        _libraryService = libraryService;
        _onPlay = onPlay;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var playlists = await _playlistService.GetPlaylistsAsync();
        var library = await _libraryService.GetLibraryAsync();
        var titleById = library.ToDictionary(v => v.VideoId, v => v.Title);

        Playlists.Clear();
        foreach (var playlist in playlists)
        {
            Playlists.Add(new PlaylistDisplay
            {
                PlaylistId = playlist.PlaylistId,
                Name = playlist.Name,
                Items = playlist.Items
                    .OrderBy(i => i.Position)
                    .Select(i => new PlaylistItemDisplay
                    {
                        VideoId = i.VideoId,
                        Title = titleById.TryGetValue(i.VideoId, out var title) ? title : "(missing video)"
                    })
                    .ToList()
            });
        }
    }

    [RelayCommand]
    private async Task CreatePlaylist()
    {
        if (string.IsNullOrWhiteSpace(NewPlaylistName)) return;
        await _playlistService.CreatePlaylistAsync(NewPlaylistName);
        NewPlaylistName = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeletePlaylist(PlaylistDisplay playlist)
    {
        await _playlistService.DeletePlaylistAsync(playlist.PlaylistId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RemoveVideo((PlaylistDisplay Playlist, Guid VideoId) args)
    {
        await _playlistService.RemoveVideoAsync(args.Playlist.PlaylistId, args.VideoId);
        await RefreshAsync();
    }

    [RelayCommand]
    private void Play((PlaylistDisplay Playlist, Guid VideoId) args) => _onPlay(args.Playlist.PlaylistId, args.VideoId);
}
```

- [ ] **Step 5: Write PlaylistsPanelView**

`src/LearnPlayAI.App/Views/Panels/PlaylistsPanelView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.Panels.PlaylistsPanelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
            <TextBox Width="220" Text="{Binding NewPlaylistName, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,8,0" />
            <Button Content="Create Playlist" Command="{Binding CreatePlaylistCommand}" Padding="12,6" />
        </StackPanel>

        <ItemsControl Grid.Row="1" ItemsSource="{Binding Playlists}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Border BorderBrush="{DynamicResource Border.Default}" BorderThickness="0,0,0,1" Padding="0,0,0,12" Margin="0,0,0,12">
                        <StackPanel>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="{Binding Name}" Foreground="{DynamicResource Text.Primary}"
                                           FontWeight="SemiBold" FontSize="16" />
                                <Button Grid.Column="1"
                                        Command="{Binding DataContext.DeletePlaylistCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}" Content="Delete Playlist" Padding="8,2" />
                            </Grid>
                            <ItemsControl ItemsSource="{Binding Items}" Margin="0,8,0,0">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <Grid Margin="0,2">
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="*" />
                                                <ColumnDefinition Width="Auto" />
                                                <ColumnDefinition Width="Auto" />
                                            </Grid.ColumnDefinitions>
                                            <TextBlock Grid.Column="0" Text="{Binding Title}" Foreground="{DynamicResource Text.Primary}"
                                                       Opacity="0.85" VerticalAlignment="Center" />
                                            <Button Grid.Column="1" Content="Play"
                                                    Click="OnPlayItemClick" Margin="8,0" Padding="8,2" />
                                            <Button Grid.Column="2" Content="Remove"
                                                    Click="OnRemoveItemClick" Padding="8,2" />
                                        </Grid>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </StackPanel>
                    </Border>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</UserControl>
```

`src/LearnPlayAI.App/Views/Panels/PlaylistsPanelView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class PlaylistsPanelView : UserControl
{
    public PlaylistsViewModel ViewModel { get; }

    public PlaylistsPanelView(PlaylistService playlistService, LibraryService libraryService, Action<Guid, Guid> onPlay)
    {
        InitializeComponent();
        ViewModel = new PlaylistsViewModel(playlistService, libraryService, onPlay);
        DataContext = ViewModel;
    }

    private void OnPlayItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlaylistItemDisplay item } button) return;
        if (FindAncestorPlaylist(button) is not { } playlist) return;
        ViewModel.PlayCommand.Execute((playlist, item.VideoId));
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PlaylistItemDisplay item } button) return;
        if (FindAncestorPlaylist(button) is not { } playlist) return;
        ViewModel.RemoveVideoCommand.Execute((playlist, item.VideoId));
    }

    private static PlaylistDisplay? FindAncestorPlaylist(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: PlaylistDisplay playlist })
            {
                return playlist;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
```

Add `using System.Windows.Media;` to the top of `PlaylistsPanelView.xaml.cs` for `VisualTreeHelper`.

- [ ] **Step 6: Wire playlists, playback-ended, and playback-error into the shell**

Replace the entire contents of `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs` with:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayerView _playerView;

    private Guid? _activePlaylistId;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlaylistService playlistService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;
        _playlistService = playlistService;
        _playerViewModel = playerViewModel;

        _themeManager.Apply(_settingsService.Current.Theme);

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        _playerViewModel.Engine.StateChanged += OnEngineStateChanged;
        _playerViewModel.Engine.ErrorOccurred += OnEngineError;

        CenterContent.Content = BuildHomePlaceholder();
    }

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.LibraryPanelView(_libraryService, _playlistService, OpenVideo);

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.PlaylistsPanelView(_playlistService, _libraryService, PlayFromPlaylist);

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.RecentPanelView(_libraryService, OpenVideo);

    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var themeLabel = new TextBlock
        {
            Text = $"Current theme: {_settingsService.Current.Theme}",
            Foreground = (Brush)FindResource("Text.Primary"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var toggleButton = new Button
        {
            Content = "Toggle theme",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toggleButton.Click += async (_, _) =>
        {
            var next = _settingsService.Current.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            await _settingsService.SetThemeAsync(next);
            _themeManager.Apply(next);
            themeLabel.Text = $"Current theme: {next}";
        };
        panel.Children.Add(themeLabel);
        panel.Children.Add(toggleButton);
        CenterContent.Content = panel;
    }

    private UIElement BuildHomePlaceholder()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select Library, Playlists, or Recent from the left, or open a file directly:",
            Foreground = (Brush)FindResource("Text.Primary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var openButton = new Button
        {
            Content = "Open Video File...",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Click += async (_, _) => await OpenFileDialogAsync();
        panel.Children.Add(openButton);
        return panel;
    }

    private async System.Threading.Tasks.Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var entry = await _libraryService.RegisterFileAsync(dialog.FileName);
        OpenVideo(entry);
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        VideoEntry? lastRegistered = null;
        foreach (var file in files)
        {
            lastRegistered = await _libraryService.RegisterFileAsync(file);
        }
        if (lastRegistered is not null)
        {
            OpenVideo(lastRegistered);
        }
    }

    private void OpenVideo(VideoEntry entry)
    {
        _activePlaylistId = null;
        OpenVideoEntry(entry);
    }

    private async void PlayFromPlaylist(Guid playlistId, Guid startVideoId)
    {
        var entry = await _libraryService.GetByIdAsync(startVideoId);
        if (entry is null) return;
        _activePlaylistId = playlistId;
        OpenVideoEntry(entry);
    }

    private void OpenVideoEntry(VideoEntry entry)
    {
        CenterContent.Content = _playerView;
        _playerView.PlayVideo(entry);
    }

    private void OnEngineStateChanged(object? sender, PlaybackState state)
    {
        if (state != PlaybackState.Ended || _activePlaylistId is null) return;
        Dispatcher.Invoke(() => _ = AdvancePlaylistAsync());
    }

    private void OnEngineError(object? sender, string message)
    {
        if (_activePlaylistId is null) return;
        Dispatcher.Invoke(() => _ = AdvancePlaylistAsync());
    }

    private async System.Threading.Tasks.Task AdvancePlaylistAsync()
    {
        if (_activePlaylistId is null) return;

        var playlist = await _playlistService.GetPlaylistAsync(_activePlaylistId.Value);
        if (playlist is null)
        {
            _activePlaylistId = null;
            return;
        }

        var ordered = playlist.Items.OrderBy(i => i.Position).ToList();
        var currentIndex = ordered.FindIndex(i => i.VideoId == _playerViewModel.CurrentVideoId);
        if (currentIndex < 0 || currentIndex + 1 >= ordered.Count)
        {
            _activePlaylistId = null;
            return;
        }

        var nextEntry = await _libraryService.GetByIdAsync(ordered[currentIndex + 1].VideoId);
        if (nextEntry is not null)
        {
            OpenVideoEntry(nextEntry);
        }
    }

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }
}
```

Add `using System.Linq;` to the top of the file alongside the other `using` statements (needed for `OrderBy`/`FindIndex` usage via LINQ's `ToList`).

- [ ] **Step 7: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: go to **Library**, use the new per-row dropdown to add two or three videos to a new playlist (create it from the **Playlists** panel first). Go to **Playlists**, click **Play** on the first item — it plays, and when it reaches the end, the next item in the playlist starts automatically. Click **Remove** on an item to take it out of the playlist. To check skip-on-error, add a corrupted/non-video file to the library (it will fail to decode) into the middle of a playlist and confirm playback advances past it instead of getting stuck.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Add Playlists panel with auto-advance and skip-on-playback-error"
```

---

### Task 20: Configurable keyboard shortcuts

**Files:**
- Modify: `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs` (add seek/volume/mute commands)
- Create: `src/LearnPlayAI.App/Input/KeyboardShortcutManager.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`
- Modify: `src/LearnPlayAI.App/App.xaml.cs`

**Interfaces:**
- Consumes: `ShortcutBinding`, `SettingsService.Shortcuts` (Task 10), `DefaultShortcuts` action ids (Task 10), `PlayerViewModel` commands (Tasks 14–16).
- Produces: `KeyboardShortcutManager.RegisterAction(string actionId, Action)`, `.Apply(Window, IReadOnlyList<ShortcutBinding>)`; `MainShellWindow.ReapplyShortcuts()`. Task 21's shortcut-rebinding UI calls `ReapplyShortcuts()` after saving a new binding.

Verified manually (keyboard focus/gesture behavior isn't practically unit-testable here).

- [ ] **Step 1: Add seek/volume/mute commands to PlayerViewModel**

In `src/LearnPlayAI.App/ViewModels/PlayerViewModel.cs`, add these fields right after the existing `_lastPersistUtc` field:

```csharp
    private double _volumeBeforeMute;
    private bool _isMuted;
```

Add these commands after the existing `SpeedDown` command method:

```csharp
    [RelayCommand]
    private void SeekForward() => Engine.Seek(TimeSpan.FromSeconds(PositionSeconds + 10));

    [RelayCommand]
    private void SeekBackward() => Engine.Seek(TimeSpan.FromSeconds(Math.Max(0, PositionSeconds - 10)));

    [RelayCommand]
    private void VolumeUp() => Volume = Math.Clamp(Volume + 0.1, 0, 1);

    [RelayCommand]
    private void VolumeDown() => Volume = Math.Clamp(Volume - 0.1, 0, 1);

    [RelayCommand]
    private void ToggleMute()
    {
        if (_isMuted)
        {
            Volume = _volumeBeforeMute;
            _isMuted = false;
        }
        else
        {
            _volumeBeforeMute = Volume;
            Volume = 0;
            _isMuted = true;
        }
    }
```

- [ ] **Step 2: Write KeyboardShortcutManager**

`src/LearnPlayAI.App/Input/KeyboardShortcutManager.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.App.Input;

public sealed class KeyboardShortcutManager
{
    private readonly Dictionary<string, Action> _actions = new();

    public void RegisterAction(string actionId, Action action) => _actions[actionId] = action;

    public void Apply(Window window, IReadOnlyList<ShortcutBinding> bindings)
    {
        window.InputBindings.Clear();
        var converter = new KeyGestureConverter();
        foreach (var binding in bindings)
        {
            if (!_actions.TryGetValue(binding.ActionId, out var action)) continue;
            if (!converter.IsValid(binding.KeyGesture)) continue;
            if (converter.ConvertFromString(binding.KeyGesture) is not KeyGesture gesture) continue;
            window.InputBindings.Add(new InputBinding(new RelayUICommand(action), gesture));
        }
    }

    private sealed class RelayUICommand : ICommand
    {
        private readonly Action _action;
        public RelayUICommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
```

- [ ] **Step 3: Wire shortcuts into the shell**

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`, replace the constructor and field declarations:

```csharp
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayerView _playerView;

    private Guid? _activePlaylistId;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlaylistService playlistService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;
        _playlistService = playlistService;
        _playerViewModel = playerViewModel;

        _themeManager.Apply(_settingsService.Current.Theme);

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        _playerViewModel.Engine.StateChanged += OnEngineStateChanged;
        _playerViewModel.Engine.ErrorOccurred += OnEngineError;

        CenterContent.Content = BuildHomePlaceholder();
    }
```

with:

```csharp
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayerView _playerView;
    private readonly Input.KeyboardShortcutManager _shortcutManager;

    private Guid? _activePlaylistId;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlaylistService playlistService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView,
        Input.KeyboardShortcutManager shortcutManager)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;
        _playlistService = playlistService;
        _playerViewModel = playerViewModel;
        _shortcutManager = shortcutManager;

        _themeManager.Apply(_settingsService.Current.Theme);

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        _playerViewModel.Engine.StateChanged += OnEngineStateChanged;
        _playerViewModel.Engine.ErrorOccurred += OnEngineError;

        RegisterShortcutActions();
        ReapplyShortcuts();

        CenterContent.Content = BuildHomePlaceholder();
    }

    private void RegisterShortcutActions()
    {
        _shortcutManager.RegisterAction("PlayPause", () => _playerViewModel.PlayPauseCommand.Execute(null));
        _shortcutManager.RegisterAction("Stop", () => _playerViewModel.StopCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekForward", () => _playerViewModel.SeekForwardCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekBackward", () => _playerViewModel.SeekBackwardCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeUp", () => _playerViewModel.VolumeUpCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeDown", () => _playerViewModel.VolumeDownCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleMute", () => _playerViewModel.ToggleMuteCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleFullScreen", ToggleFullScreen);
        _shortcutManager.RegisterAction("StepFrame", () => _playerViewModel.StepFrameCommand.Execute(null));
        _shortcutManager.RegisterAction("Screenshot", () => _playerViewModel.ScreenshotCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedUp", () => _playerViewModel.SpeedUpCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedDown", () => _playerViewModel.SpeedDownCommand.Execute(null));
    }

    public void ReapplyShortcuts() => _shortcutManager.Apply(this, _settingsService.Shortcuts);
```

- [ ] **Step 4: Register `KeyboardShortcutManager` in the composition root**

In `src/LearnPlayAI.App/App.xaml.cs`, replace:

```csharp
        services.AddSingleton<ViewModels.PlayerViewModel>();
        services.AddSingleton<Views.TransportBarView>();
        services.AddSingleton<Views.MainShellWindow>();
```

with:

```csharp
        services.AddSingleton<ViewModels.PlayerViewModel>();
        services.AddSingleton<Views.TransportBarView>();
        services.AddSingleton<Input.KeyboardShortcutManager>();
        services.AddSingleton<Views.MainShellWindow>();
```

- [ ] **Step 5: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected, with a video open and the window focused: **Space** toggles play/pause; **Left**/**Right** seek back/forward 10s; **Up**/**Down** change volume; **M** mutes/unmutes; **F** toggles full screen; **.** steps one frame; **Ctrl+Shift+S** takes a screenshot; **+**/**-** change speed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Add configurable keyboard shortcuts for all playback actions"
```

---

### Task 21: Settings panel — theme, default volume, shortcut rebinding

**Files:**
- Create: `src/LearnPlayAI.App/ViewModels/SettingsViewModel.cs`
- Create: `src/LearnPlayAI.App/Views/Panels/SettingsPanelView.xaml`
- Create: `src/LearnPlayAI.App/Views/Panels/SettingsPanelView.xaml.cs`
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `SettingsService` (Task 10), `ThemeManager` (Task 13), `MainShellWindow.ReapplyShortcuts()` (Task 20).
- Produces: `SettingsViewModel`, `SettingsPanelView`, replacing the temporary inline theme-toggle button from Task 13 with the real Settings panel. No later task depends on these.

Verified manually.

- [ ] **Step 1: Write SettingsViewModel**

`src/LearnPlayAI.App/ViewModels/SettingsViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.App.Services;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly Action _onShortcutsChanged;

    [ObservableProperty] private ThemeMode theme;
    [ObservableProperty] private double defaultVolume;

    public ObservableCollection<ShortcutBinding> Shortcuts { get; } = new();

    public SettingsViewModel(SettingsService settingsService, ThemeManager themeManager, Action onShortcutsChanged)
    {
        _settingsService = settingsService;
        _themeManager = themeManager;
        _onShortcutsChanged = onShortcutsChanged;

        theme = settingsService.Current.Theme;
        defaultVolume = settingsService.Current.DefaultVolume;
        foreach (var binding in settingsService.Shortcuts)
        {
            Shortcuts.Add(binding);
        }
    }

    [RelayCommand]
    private async Task ToggleTheme()
    {
        Theme = Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        await _settingsService.SetThemeAsync(Theme);
        _themeManager.Apply(Theme);
    }

    public async Task RebindAsync(ShortcutBinding binding, string newGesture)
    {
        await _settingsService.SetShortcutAsync(binding.ActionId, newGesture);
        binding.KeyGesture = newGesture;
        _onShortcutsChanged();
    }

    partial void OnDefaultVolumeChanged(double value) => _ = _settingsService.SetDefaultVolumeAsync(value);
}
```

- [ ] **Step 2: Write SettingsPanelView**

`src/LearnPlayAI.App/Views/Panels/SettingsPanelView.xaml`:

```xml
<UserControl x:Class="LearnPlayAI.App.Views.Panels.SettingsPanelView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Margin="24">
        <TextBlock Text="Appearance" FontSize="16" FontWeight="SemiBold" Foreground="{DynamicResource Text.Primary}" Margin="0,0,0,8" />
        <StackPanel Orientation="Horizontal" Margin="0,0,0,24">
            <TextBlock Text="{Binding Theme, StringFormat=Current theme: {0}}" Foreground="{DynamicResource Text.Primary}"
                       VerticalAlignment="Center" Margin="0,0,12,0" />
            <Button Content="Toggle theme" Command="{Binding ToggleThemeCommand}" Padding="12,6" />
        </StackPanel>

        <TextBlock Text="Playback" FontSize="16" FontWeight="SemiBold" Foreground="{DynamicResource Text.Primary}" Margin="0,0,0,8" />
        <StackPanel Orientation="Horizontal" Margin="0,0,0,24">
            <TextBlock Text="Default volume" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" Margin="0,0,12,0" />
            <Slider Width="160" Minimum="0" Maximum="1" Value="{Binding DefaultVolume}" VerticalAlignment="Center" />
        </StackPanel>

        <TextBlock Text="Keyboard Shortcuts" FontSize="16" FontWeight="SemiBold" Foreground="{DynamicResource Text.Primary}" Margin="0,0,0,8" />
        <ItemsControl ItemsSource="{Binding Shortcuts}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="180" />
                            <ColumnDefinition Width="120" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="{Binding ActionId}" Foreground="{DynamicResource Text.Primary}" VerticalAlignment="Center" />
                        <TextBlock Grid.Column="1" Text="{Binding KeyGesture}" Foreground="{DynamicResource Text.Primary}" Opacity="0.8" VerticalAlignment="Center" />
                        <Button Grid.Column="2" Content="Rebind..." Click="OnRebindClick" Padding="8,2" />
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </StackPanel>
</UserControl>
```

`src/LearnPlayAI.App/Views/Panels/SettingsPanelView.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class SettingsPanelView : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPanelView(SettingsService settingsService, ThemeManager themeManager, Action onShortcutsChanged)
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(settingsService, themeManager, onShortcutsChanged);
        DataContext = ViewModel;
    }

    private async void OnRebindClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ShortcutBinding binding }) return;

        var window = new Window
        {
            Title = $"Press a key for \"{binding.ActionId}\"",
            Width = 360,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Content = new TextBlock
            {
                Text = "Press any key combination...",
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        string? capturedGesture = null;
        window.PreviewKeyDown += (_, keyArgs) =>
        {
            var key = keyArgs.Key == Key.System ? keyArgs.SystemKey : keyArgs.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return;

            var gesture = new KeyGesture(key, Keyboard.Modifiers);
            capturedGesture = new KeyGestureConverter().ConvertToString(gesture);
            window.Close();
        };

        window.ShowDialog();

        if (capturedGesture is not null)
        {
            await ViewModel.RebindAsync(binding, capturedGesture);
        }
    }
}
```

- [ ] **Step 3: Replace the temporary theme toggle with the real Settings panel**

In `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`, replace:

```csharp
    private void OnNavSettings(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var themeLabel = new TextBlock
        {
            Text = $"Current theme: {_settingsService.Current.Theme}",
            Foreground = (Brush)FindResource("Text.Primary"),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var toggleButton = new Button
        {
            Content = "Toggle theme",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        toggleButton.Click += async (_, _) =>
        {
            var next = _settingsService.Current.Theme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            await _settingsService.SetThemeAsync(next);
            _themeManager.Apply(next);
            themeLabel.Text = $"Current theme: {next}";
        };
        panel.Children.Add(themeLabel);
        panel.Children.Add(toggleButton);
        CenterContent.Content = panel;
    }
```

with:

```csharp
    private void OnNavSettings(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.SettingsPanelView(_settingsService, _themeManager, ReapplyShortcuts);
```

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected: **Settings** shows the theme toggle (still works), a default-volume slider (new videos open at this volume), and a list of all 12 shortcuts. Click **Rebind...** next to "PlayPause", press a different key (e.g. `P`), and confirm the list updates to show the new gesture. Close and reopen the app — the custom binding should have replaced the default (verify by pressing `P` instead of Space while a video plays, and confirming Space no longer does anything).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Settings panel: theme, default volume, and shortcut rebinding"
```

---

### Task 22: Error-handling polish — non-blocking toast and missing-file relocate/remove

**Files:**
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml` (add a toast overlay)
- Modify: `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs`

**Interfaces:**
- Consumes: `LibraryService.MarkMissingAsync/RemoveAsync/RegisterFileAsync` (Task 8), `IPlayerEngine.ErrorOccurred` (Task 11), `PlaylistService.GetPlaylistAsync` (Task 9).
- Produces: `MainShellWindow.ShowToast(string)`, a relocate/remove flow triggered whenever a Library or Recent entry's file can no longer be found, and non-blocking error toasts on playback failures (in addition to the skip-to-next-item behavior already wired in Task 19). This is the last task that modifies `MainShellWindow`.

Verified manually (rename/delete a registered video's file on disk, then try to play it from Library or Recent).

- [ ] **Step 1: Add a toast overlay to the shell**

Replace the entire contents of `src/LearnPlayAI.App/Views/MainShellWindow.xaml` with:

```xml
<Window x:Class="LearnPlayAI.App.Views.MainShellWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LearnPlay AI" Height="800" Width="1280"
        Background="{DynamicResource App.Background}"
        AllowDrop="True" Drop="OnWindowDrop">
    <Grid>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="*" />
                <RowDefinition Height="90" />
            </Grid.RowDefinitions>

            <Grid Grid.Row="0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="220" MinWidth="160" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="280" MinWidth="200" />
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0" Background="{DynamicResource Sidebar.Background}">
                    <Button Content="Library" Click="OnNavLibrary" Style="{StaticResource NavButtonStyle}" />
                    <Button Content="Playlists" Click="OnNavPlaylists" Style="{StaticResource NavButtonStyle}" />
                    <Button Content="Recent" Click="OnNavRecent" Style="{StaticResource NavButtonStyle}" />
                    <Button Content="Notes" IsEnabled="False" Style="{StaticResource NavButtonStyle}" />
                    <Button Content="Settings" Click="OnNavSettings" Style="{StaticResource NavButtonStyle}" />
                </StackPanel>

                <ContentControl x:Name="CenterContent" Grid.Column="1" />

                <Border Grid.Column="2" Background="{DynamicResource Sidebar.Background}">
                    <TextBlock Text="AI Assistant and live notes arrive in a later phase."
                               Foreground="{DynamicResource Text.Primary}"
                               Margin="16" Opacity="0.6" TextWrapping="Wrap" />
                </Border>
            </Grid>

            <Border x:Name="TransportBarHost" Grid.Row="1" Background="{DynamicResource Sidebar.Background}"
                    BorderBrush="{DynamicResource Border.Default}" BorderThickness="0,1,0,0" />
        </Grid>

        <Border x:Name="ToastBorder" Background="#CC323232" CornerRadius="6" Padding="12,8"
                HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,24,110"
                Visibility="Collapsed">
            <TextBlock x:Name="ToastText" Foreground="White" TextWrapping="Wrap" MaxWidth="360" />
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Add the toast method, missing-file flow, and wire them in**

Replace the entire contents of `src/LearnPlayAI.App/Views/MainShellWindow.xaml.cs` with:

```csharp
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LearnPlayAI.App.Services;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;
using LearnPlayAI.Core.Services;
using Microsoft.Win32;

namespace LearnPlayAI.App.Views;

public partial class MainShellWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly ThemeManager _themeManager;
    private readonly LibraryService _libraryService;
    private readonly PlaylistService _playlistService;
    private readonly PlayerViewModel _playerViewModel;
    private readonly PlayerView _playerView;
    private readonly Input.KeyboardShortcutManager _shortcutManager;

    private Guid? _activePlaylistId;
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private bool _isFullScreen;
    private DispatcherTimer? _toastTimer;

    public MainShellWindow(
        SettingsService settingsService,
        ThemeManager themeManager,
        LibraryService libraryService,
        PlaylistService playlistService,
        PlayerViewModel playerViewModel,
        TransportBarView transportBarView,
        Input.KeyboardShortcutManager shortcutManager)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _themeManager = themeManager;
        _libraryService = libraryService;
        _playlistService = playlistService;
        _playerViewModel = playerViewModel;
        _shortcutManager = shortcutManager;

        _themeManager.Apply(_settingsService.Current.Theme);

        _playerView = new PlayerView(playerViewModel);
        TransportBarHost.Child = transportBarView;

        _playerViewModel.Engine.StateChanged += OnEngineStateChanged;
        _playerViewModel.Engine.ErrorOccurred += OnEngineError;

        RegisterShortcutActions();
        ReapplyShortcuts();

        CenterContent.Content = BuildHomePlaceholder();
    }

    private void RegisterShortcutActions()
    {
        _shortcutManager.RegisterAction("PlayPause", () => _playerViewModel.PlayPauseCommand.Execute(null));
        _shortcutManager.RegisterAction("Stop", () => _playerViewModel.StopCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekForward", () => _playerViewModel.SeekForwardCommand.Execute(null));
        _shortcutManager.RegisterAction("SeekBackward", () => _playerViewModel.SeekBackwardCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeUp", () => _playerViewModel.VolumeUpCommand.Execute(null));
        _shortcutManager.RegisterAction("VolumeDown", () => _playerViewModel.VolumeDownCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleMute", () => _playerViewModel.ToggleMuteCommand.Execute(null));
        _shortcutManager.RegisterAction("ToggleFullScreen", ToggleFullScreen);
        _shortcutManager.RegisterAction("StepFrame", () => _playerViewModel.StepFrameCommand.Execute(null));
        _shortcutManager.RegisterAction("Screenshot", () => _playerViewModel.ScreenshotCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedUp", () => _playerViewModel.SpeedUpCommand.Execute(null));
        _shortcutManager.RegisterAction("SpeedDown", () => _playerViewModel.SpeedDownCommand.Execute(null));
    }

    public void ReapplyShortcuts() => _shortcutManager.Apply(this, _settingsService.Shortcuts);

    private void OnNavLibrary(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.LibraryPanelView(_libraryService, _playlistService, OpenVideo);

    private void OnNavPlaylists(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.PlaylistsPanelView(_playlistService, _libraryService, PlayFromPlaylist);

    private void OnNavRecent(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.RecentPanelView(_libraryService, OpenVideo);

    private void OnNavSettings(object sender, RoutedEventArgs e) =>
        CenterContent.Content = new Panels.SettingsPanelView(_settingsService, _themeManager, ReapplyShortcuts);

    private UIElement BuildHomePlaceholder()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Select Library, Playlists, or Recent from the left, or open a file directly:",
            Foreground = (Brush)FindResource("Text.Primary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var openButton = new Button
        {
            Content = "Open Video File...",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Click += async (_, _) => await OpenFileDialogAsync();
        panel.Children.Add(openButton);
        return panel;
    }

    private async System.Threading.Tasks.Task OpenFileDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var entry = await _libraryService.RegisterFileAsync(dialog.FileName);
        OpenVideo(entry);
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        VideoEntry? lastRegistered = null;
        foreach (var file in files)
        {
            lastRegistered = await _libraryService.RegisterFileAsync(file);
        }
        if (lastRegistered is not null)
        {
            OpenVideo(lastRegistered);
        }
    }

    private async void OpenVideo(VideoEntry entry)
    {
        _activePlaylistId = null;
        if (!await EnsureFileAvailableAsync(entry)) return;
        OpenVideoEntry(entry);
    }

    private async void PlayFromPlaylist(Guid playlistId, Guid startVideoId)
    {
        _activePlaylistId = playlistId;
        var playlist = await _playlistService.GetPlaylistAsync(playlistId);
        if (playlist is null)
        {
            _activePlaylistId = null;
            return;
        }

        var ordered = playlist.Items.OrderBy(i => i.Position).ToList();
        var startIndex = ordered.FindIndex(i => i.VideoId == startVideoId);
        if (startIndex < 0) startIndex = 0;

        for (var i = startIndex; i < ordered.Count; i++)
        {
            var entry = await _libraryService.GetByIdAsync(ordered[i].VideoId);
            if (entry is null) continue;
            if (!await EnsureFileAvailableAsync(entry)) continue;
            OpenVideoEntry(entry);
            return;
        }

        _activePlaylistId = null;
        ShowToast("No playable videos remain in this playlist.");
    }

    private void OpenVideoEntry(VideoEntry entry)
    {
        CenterContent.Content = _playerView;
        _playerView.PlayVideo(entry);
    }

    private async System.Threading.Tasks.Task<bool> EnsureFileAvailableAsync(VideoEntry entry)
    {
        if (File.Exists(entry.FilePath)) return true;

        await _libraryService.MarkMissingAsync(entry.VideoId);

        var result = MessageBox.Show(
            $"\"{entry.Title}\" could not be found at:\n{entry.FilePath}\n\nLocate the file? (No removes it from your library)",
            "Video missing", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm|All files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                var relocated = await _libraryService.RegisterFileAsync(dialog.FileName);
                entry.FilePath = relocated.FilePath;
                entry.IsMissing = false;
                return true;
            }
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            await _libraryService.RemoveAsync(entry.VideoId);
        }

        return false;
    }

    private void OnEngineStateChanged(object? sender, PlaybackState state)
    {
        if (state != PlaybackState.Ended || _activePlaylistId is null) return;
        Dispatcher.Invoke(() => _ = AdvancePlaylistAsync());
    }

    private void OnEngineError(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            ShowToast($"Playback error: {message}");
            if (_activePlaylistId is not null)
            {
                _ = AdvancePlaylistAsync();
            }
        });
    }

    private async System.Threading.Tasks.Task AdvancePlaylistAsync()
    {
        if (_activePlaylistId is null) return;

        var playlist = await _playlistService.GetPlaylistAsync(_activePlaylistId.Value);
        if (playlist is null)
        {
            _activePlaylistId = null;
            return;
        }

        var ordered = playlist.Items.OrderBy(i => i.Position).ToList();
        var currentIndex = ordered.FindIndex(i => i.VideoId == _playerViewModel.CurrentVideoId);
        if (currentIndex < 0 || currentIndex + 1 >= ordered.Count)
        {
            _activePlaylistId = null;
            return;
        }

        var nextEntry = await _libraryService.GetByIdAsync(ordered[currentIndex + 1].VideoId);
        if (nextEntry is not null)
        {
            OpenVideoEntry(nextEntry);
        }
    }

    public void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
            _toastTimer!.Stop();
        };
        _toastTimer.Start();
    }

    public void ToggleFullScreen()
    {
        if (!_isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            _isFullScreen = false;
        }
    }
}
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet build src/LearnPlayAI.App/LearnPlayAI.App.csproj
dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj
```

Expected:
1. Add a video to the Library, then rename or delete its file on disk outside the app. Click **Play** on it from Library (or **Resume** from Recent) — a dialog reports it's missing; choose **Yes** and pick a new location to confirm it plays from the relocated path, or choose **No** to confirm it's removed from the Library list.
2. Play a corrupted/non-media file (e.g. rename a `.txt` file to `.mp4` and add it) — confirm a toast appears in the bottom-right reading "Playback error: ..." and automatically disappears after a few seconds, without any blocking dialog.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Add non-blocking error toasts and missing-file relocate/remove flow"
```

---

### Task 23: Final smoke test and README

**Files:**
- Create: `README.md`

**Interfaces:**
- Consumes: the entire Phase 1 feature set (Tasks 1–22).
- Produces: a documented, manually-verified build; no code consumed by later work (Phase 2 starts a new spec/plan).

- [ ] **Step 1: Run the full automated test suite**

```bash
dotnet test
```

Expected: all Core and Data tests pass (VideoHasher, DatabaseBootstrapper, the three SQLite repositories, LibraryService, PlaylistService, SettingsService, PlaybackRate — roughly 35 tests across both test projects).

- [ ] **Step 2: Write the README**

`README.md`:

```markdown
# LearnPlay AI — Phase 1 (Video Player)

A native Windows video player, built as the foundation for LearnPlay AI's later
AI-assisted learning features (transcription, notes, diagrams, chat — each a
separate phase).

## Prerequisites

- Windows 10/11
- .NET 8 SDK

## Build and run

    dotnet build
    dotnet run --project src/LearnPlayAI.App/LearnPlayAI.App.csproj

## Run the automated tests

    dotnet test

## Project layout

- `src/LearnPlayAI.Core` — domain models, repository interfaces, services, and the
  LibVLCSharp-based player engine. No WPF or SQLite dependency; unit-tested in
  isolation.
- `src/LearnPlayAI.Data` — SQLite implementations of Core's repository interfaces.
- `src/LearnPlayAI.App` — the WPF application: views, view models, themes, and
  keyboard shortcut handling.
- `tests/LearnPlayAI.Core.Tests` — tests against fakes/stubs, no SQLite involved.
- `tests/LearnPlayAI.Data.Tests` — tests against real (temporary) SQLite databases.

Application data (the SQLite database) is stored at
`%LOCALAPPDATA%\LearnPlayAI\learnplay.db`.

## Feature checklist (Phase 1 scope)

- [x] Play/pause/stop/resume, seek, variable speed (0.25x–4x), frame-stepping
- [x] MP4/MKV/AVI/MOV/WMV/FLV/WebM and other libVLC-supported formats
- [x] External and embedded subtitles, multiple audio/subtitle tracks, subtitle sync
- [x] Brightness/contrast/saturation adjustment
- [x] Screenshot capture, full-screen mode
- [x] Playlists (create/rename/delete, add/remove videos, auto-advance, skip-on-error)
- [x] Drag-and-drop, Library, Recently Watched, resume-from-last-position
- [x] Configurable keyboard shortcuts
- [x] Dark/light themes
- [x] Non-blocking error toasts; missing-file relocate/remove flow
- [ ] AI notes, transcription, diagrams, chat — out of scope for Phase 1

## Manual smoke test (run before considering Phase 1 done)

1. Launch the app; confirm the shell loads with the left nav, empty center pane,
   and the "AI Assistant" placeholder on the right.
2. Drag an MP4 onto the window — it registers in the Library and starts playing.
3. Verify play/pause, stop, seeking via the scrubber, and the volume slider.
4. Verify speed control (0.25x–4x, clamped), frame-stepping while paused, and
   screenshot capture (check `Pictures\LearnPlayAI\`).
5. Verify full-screen toggle restores the previous window state when exited.
6. With a multi-track video, verify audio/subtitle track switching and subtitle
   sync offset.
7. Verify brightness/contrast/saturation sliders visibly affect the picture.
8. Add a few videos to the Library; create a playlist and add them to it via the
   per-row playlist picker; play the playlist and confirm auto-advance to the next
   item on natural end, and skip-to-next on a corrupted item.
9. Stop partway through a video, revisit it from Recent, and confirm it resumes
   at the correct position.
10. Rename/delete a registered video's file on disk; confirm the relocate/remove
    dialog appears when trying to play it, and both paths (relocate, remove) work.
11. Toggle the theme from Settings; confirm it persists across an app restart.
12. Rebind a keyboard shortcut from Settings and confirm the new key works (and
    the old one no longer does).
13. Confirm every action above works with a 4K source file without dropped-frame
    stutter on typical hardware (hardware-accelerated decode via libVLC).
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "Add README with setup instructions and Phase 1 manual smoke test checklist"
```

---

## Self-Review Notes

- **Spec coverage:** every bullet in the parent design spec's "Feature checklist" (Phase 1 scope, section 2 of the original project spec) maps to a task: playback core (14), speed/frame/screenshot/fullscreen (15), tracks/sync/adjustments (16), drag-and-drop/library (17), recent/resume (18), playlists (19), shortcuts (20), themes (13, 21), persistence/`VideoId` identity (3–8), error handling (22). The `VideoId` content-hash identity model required by the parent spec for future-phase note/transcript attachment is established in Task 3 and used throughout.
- **Placeholder scan:** no "TBD"/"TODO" remain; the only intentional placeholders are the inert Notes/AI Assistant panels, which the parent spec explicitly defers to later phases.
- **Type consistency:** verified `IPlayerEngine`/`LibVlcPlayerEngine` member names (`Open`, `Play`, `Pause`, `Stop`, `Seek`, `StepFrame`, `SetRate`, `Volume`, `SetAudioTrack`, `SetSubtitleTrack`, `SetSubtitleDelay`, `SetVideoAdjust`, `Screenshot`, `AudioTracks`, `SubtitleTracks`) are used identically across Tasks 11, 14, 15, 16, and 20; `PlayerViewModel` command names referenced by `KeyboardShortcutManager` registrations in Task 20 match the `[RelayCommand]`-generated command properties from Tasks 14–16, 20 exactly (`PlayPauseCommand`, `StopCommand`, `SeekForwardCommand`, `SeekBackwardCommand`, `VolumeUpCommand`, `VolumeDownCommand`, `ToggleMuteCommand`, `StepFrameCommand`, `ScreenshotCommand`, `SpeedUpCommand`, `SpeedDownCommand`); repository interface method names in Task 2 match every call site in Tasks 5–10.
- **Scope check:** this plan covers Phase 1 only (native player + shell), as scoped during brainstorming. Phases 2–8 (transcription, AI notes, diagrams, screenshots/OCR, chat, notes editor/export, packaging) are explicitly out of scope and will each get their own spec → plan cycle.
