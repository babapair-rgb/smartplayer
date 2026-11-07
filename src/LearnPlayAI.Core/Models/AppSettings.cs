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
