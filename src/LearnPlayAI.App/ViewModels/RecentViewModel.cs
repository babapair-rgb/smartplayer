using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.ViewModels;

public partial class RecentViewModel : ObservableObject
{
    private readonly LibraryService _libraryService;
    private readonly Action<VideoEntry> _onOpen;

    public ObservableCollection<VideoEntry> Items { get; } = new();

    public RecentViewModel(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        _libraryService = libraryService;
        _onOpen = onOpen;
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        Items.Clear();
        foreach (var entry in await _libraryService.GetRecentAsync())
        {
            Items.Add(entry);
        }
    }

    [RelayCommand]
    private void Open(VideoEntry entry) => _onOpen(entry);
}
