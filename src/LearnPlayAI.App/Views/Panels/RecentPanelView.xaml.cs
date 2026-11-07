using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class RecentPanelView : UserControl
{
    public RecentViewModel ViewModel { get; }

    public RecentPanelView(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new RecentViewModel(libraryService, onOpen);
        DataContext = ViewModel;
    }
}
