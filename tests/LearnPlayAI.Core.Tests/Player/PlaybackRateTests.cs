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
