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
}
