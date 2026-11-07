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
        insert.CommandText = "INSERT OR IGNORE INTO playlist_items (playlist_id, video_id, position) VALUES ($pid, $vid, $pos);";
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
