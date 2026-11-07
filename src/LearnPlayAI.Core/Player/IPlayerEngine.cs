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
    float Volume { get; set; }

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
