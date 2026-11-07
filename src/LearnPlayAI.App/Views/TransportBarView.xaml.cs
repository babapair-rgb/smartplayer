using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LearnPlayAI.App.ViewModels;

namespace LearnPlayAI.App.Views;

public partial class TransportBarView : UserControl
{
    public TransportBarView(PlayerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnScrubberPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PlayerViewModel viewModel)
        {
            viewModel.SeekCommand.Execute(ScrubberSlider.Value);
        }
    }

    private void OnFullScreenClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is Views.MainShellWindow shell)
        {
            shell.ToggleFullScreen();
        }
    }

    private void OnAudioTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ValueTuple<int, string> track } && DataContext is PlayerViewModel viewModel)
        {
            viewModel.SelectAudioTrackCommand.Execute(track.Item1);
        }
    }

    private void OnSubtitleTrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ValueTuple<int, string> track } && DataContext is PlayerViewModel viewModel)
        {
            viewModel.SelectSubtitleTrackCommand.Execute(track.Item1);
        }
    }
}
