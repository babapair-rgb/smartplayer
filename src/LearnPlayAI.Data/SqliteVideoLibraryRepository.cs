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
