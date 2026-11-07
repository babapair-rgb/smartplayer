using System.Windows;
using System.Windows.Input;
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.App.Input;

public sealed class KeyboardShortcutManager
{
    private readonly Dictionary<string, Action> _actions = new();
    private Dictionary<(Key Key, ModifierKeys Modifiers), Action> _bindings = new();
    private Window? _attachedWindow;

    public void RegisterAction(string actionId, Action action) => _actions[actionId] = action;

    public void Apply(Window window, IReadOnlyList<ShortcutBinding> bindings)
    {
        if (_attachedWindow is not null)
        {
            _attachedWindow.PreviewKeyDown -= OnPreviewKeyDown;
        }

        _bindings = new Dictionary<(Key, ModifierKeys), Action>();
        foreach (var binding in bindings)
        {
            if (!_actions.TryGetValue(binding.ActionId, out var action)) continue;
            if (!TryParseGesture(binding.KeyGesture, out var key, out var modifiers)) continue;
            _bindings[(key, modifiers)] = action;
        }

        _attachedWindow = window;
        window.PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_bindings.TryGetValue((key, Keyboard.Modifiers), out var action))
        {
            action();
            e.Handled = true;
        }
    }

    private static bool TryParseGesture(string gesture, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "shift" => ModifierKeys.Shift,
                "alt" => ModifierKeys.Alt,
                "win" or "windows" => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
        }

        return Enum.TryParse(parts[^1], ignoreCase: true, out key);
    }
}
