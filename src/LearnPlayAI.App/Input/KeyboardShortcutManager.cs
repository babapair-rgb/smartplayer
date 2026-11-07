using System.Windows;
using System.Windows.Input;
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.App.Input;

public sealed class KeyboardShortcutManager
{
    private readonly Dictionary<string, Action> _actions = new();

    public void RegisterAction(string actionId, Action action) => _actions[actionId] = action;

    public void Apply(Window window, IReadOnlyList<ShortcutBinding> bindings)
    {
        window.InputBindings.Clear();
        var converter = new KeyGestureConverter();
        foreach (var binding in bindings)
        {
            if (!_actions.TryGetValue(binding.ActionId, out var action)) continue;
            if (!converter.IsValid(binding.KeyGesture)) continue;
            if (converter.ConvertFromString(binding.KeyGesture) is not KeyGesture gesture) continue;
            window.InputBindings.Add(new InputBinding(new RelayUICommand(action), gesture));
        }
    }

    private sealed class RelayUICommand : ICommand
    {
        private readonly Action _action;
        public RelayUICommand(Action action) => _action = action;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _action();
    }
}
