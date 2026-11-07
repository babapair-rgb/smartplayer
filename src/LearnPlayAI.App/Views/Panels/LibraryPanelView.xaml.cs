using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App.Views.Panels;

public partial class LibraryPanelView : UserControl
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPanelView(LibraryService libraryService, Action<VideoEntry> onOpen)
    {
        InitializeComponent();
        ViewModel = new LibraryViewModel(libraryService, onOpen);
        DataContext = ViewModel;
    }
}
