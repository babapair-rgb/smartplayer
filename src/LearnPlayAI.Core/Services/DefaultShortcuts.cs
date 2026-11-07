using LearnPlayAI.Core.Models;

namespace LearnPlayAI.Core.Services;

public static class DefaultShortcuts
{
    public static IReadOnlyList<ShortcutBinding> Create() => new List<ShortcutBinding>
    {
        new() { ActionId = "PlayPause", KeyGesture = "Space" },
        new() { ActionId = "Stop", KeyGesture = "Ctrl+Space" },
        new() { ActionId = "SeekForward", KeyGesture = "Right" },
        new() { ActionId = "SeekBackward", KeyGesture = "Left" },
        new() { ActionId = "VolumeUp", KeyGesture = "Up" },
        new() { ActionId = "VolumeDown", KeyGesture = "Down" },
        new() { ActionId = "ToggleMute", KeyGesture = "M" },
        new() { ActionId = "ToggleFullScreen", KeyGesture = "F" },
        new() { ActionId = "StepFrame", KeyGesture = "OemPeriod" },
        new() { ActionId = "Screenshot", KeyGesture = "Ctrl+Shift+S" },
        new() { ActionId = "SpeedUp", KeyGesture = "OemPlus" },
        new() { ActionId = "SpeedDown", KeyGesture = "OemMinus" },
    };
}
