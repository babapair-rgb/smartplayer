namespace LearnPlayAI.Core.Player;

public static class PlaybackRate
{
    public const float Minimum = 0.25f;
    public const float Maximum = 4.0f;

    public static float Clamp(float requested) => Math.Clamp(requested, Minimum, Maximum);
}
