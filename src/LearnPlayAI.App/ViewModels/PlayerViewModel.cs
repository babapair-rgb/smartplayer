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
    private double _volumeBeforeMute;
    private bool _isMuted;
    private TimeSpan? _pendingSeek;

    [ObservableProperty] private bool isPlaying;
    [ObservableProperty] private double positionSeconds;
    [ObservableProperty] private double durationSeconds;
    [ObservableProperty] private string currentTitle = "No video loaded";
    [ObservableProperty] private double volume;
    [ObservableProperty] private float rate = 1.0f;
    [ObservableProperty] private IReadOnlyList<TrackInfo> audioTracks = Array.Empty<TrackInfo>();
    [ObservableProperty] private IReadOnlyList<TrackInfo> subtitleTracks = Array.Empty<TrackInfo>();
    [ObservableProperty] private double subtitleDelaySeconds;
    [ObservableProperty] private float brightness = 1.0f;
    [ObservableProperty] private float contrast = 1.0f;
    [ObservableProperty] private float saturation = 1.0f;

    public IPlayerEngine Engine { get; }
    public Guid? CurrentVideoId => _currentEntry?.VideoId;

    public PlayerViewModel(IPlayerEngine engine, LibraryService libraryService, SettingsService settingsService)
    {
        Engine = engine;
        _libraryService = libraryService;
        volume = settingsService.Current.DefaultVolume;
        Engine.Volume = (float)volume;

        Engine.PositionChanged += (_, pos) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() => PositionSeconds = pos.TotalSeconds);
            MaybePersistPosition(pos);
        };
        Engine.DurationChanged += (_, dur) =>
        {
            Application.Current.Dispatcher.BeginInvoke(() => DurationSeconds = dur.TotalSeconds);
            if (_currentEntry is not null)
            {
                _ = _libraryService.UpdateDurationAsync(_currentEntry.VideoId, dur);
            }
        };
        Engine.StateChanged += (_, state) => Application.Current.Dispatcher.BeginInvoke(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            if (state == PlaybackState.Playing)
            {
                AudioTracks = Engine.AudioTracks;
                SubtitleTracks = Engine.SubtitleTracks;

                if (_pendingSeek is { } seekTarget)
                {
                    Engine.Seek(seekTarget);
                    _pendingSeek = null;
                }
            }
            else if (state == PlaybackState.Ended && _currentEntry is not null)
            {
                _ = _libraryService.UpdatePlaybackPositionAsync(_currentEntry.VideoId, TimeSpan.Zero);
            }
        });
    }

    public void LoadVideo(VideoEntry entry)
    {
        _currentEntry = entry;
        CurrentTitle = entry.Title;
        Engine.Open(entry.FilePath);
        _pendingSeek = entry.LastPosition > TimeSpan.Zero ? entry.LastPosition : null;
        Engine.Play();
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
