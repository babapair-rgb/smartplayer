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

    public float Volume
    {
        get => _mediaPlayer.Volume / 100f;
        set => _mediaPlayer.Volume = (int)Math.Clamp(value * 100f, 0f, 100f);
    }

    public IReadOnlyList<(int Id, string Name)> AudioTracks { get; private set; } = Array.Empty<(int, string)>();
    public IReadOnlyList<(int Id, string Name)> SubtitleTracks { get; private set; } = Array.Empty<(int, string)>();

    public MediaPlayer NativeMediaPlayer => _mediaPlayer;

    public LibVlcPlayerEngine()
    {
        LibVLCSharp.Shared.Core.Initialize();
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
