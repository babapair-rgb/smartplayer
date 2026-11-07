using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LearnPlayAI.Core.Models;

public sealed class ShortcutBinding : INotifyPropertyChanged
{
    private string _keyGesture = string.Empty;

    public required string ActionId { get; init; }

    public required string KeyGesture
    {
        get => _keyGesture;
        set
        {
            if (_keyGesture == value) return;
            _keyGesture = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
