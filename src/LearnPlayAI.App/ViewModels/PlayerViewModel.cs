using System.IO;
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
    [ObservableProperty] private float rate = 1.0f;
    [ObservableProperty] private IReadOnlyList<(int Id, string Name)> audioTracks = Array.Empty<(int, string)>();
    [ObservableProperty] private IReadOnlyList<(int Id, string Name)> subtitleTracks = Array.Empty<(int, string)>();
    [ObservableProperty] private double subtitleDelaySeconds;
    [ObservableProperty] private float brightness = 1.0f;
    [ObservableProperty] private float contrast = 1.0f;
    [ObservableProperty] private float saturation = 1.0f;

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
        Engine.StateChanged += (_, state) => Application.Current.Dispatcher.Invoke(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            if (state == PlaybackState.Playing)
            {
                AudioTracks = Engine.AudioTracks;
                SubtitleTracks = Engine.SubtitleTracks;
            }
        });
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

    partial void OnVolumeChanged(double value) => Engine.Volume = (float)value;
}
