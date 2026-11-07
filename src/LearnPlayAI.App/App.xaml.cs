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
        services.AddSingleton<Services.ThemeManager>();
        services.AddSingleton<Views.MainShellWindow>();

        Services = services.BuildServiceProvider();

        var settingsService = Services.GetRequiredService<SettingsService>();
        settingsService.InitializeAsync().GetAwaiter().GetResult();

        var mainWindow = Services.GetRequiredService<Views.MainShellWindow>();
        mainWindow.Show();
    }
}
