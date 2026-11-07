using System.Windows.Controls;
using LearnPlayAI.App.ViewModels;
using LearnPlayAI.Core.Models;
using LearnPlayAI.Core.Player;

namespace LearnPlayAI.App.Views;

public partial class PlayerView : UserControl
{
    private readonly PlayerViewModel _viewModel;

    public PlayerView(PlayerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        if (_viewModel.Engine is LibVlcPlayerEngine libVlc)
        {
            VideoViewControl.MediaPlayer = libVlc.NativeMediaPlayer;
        }
    }

    public void PlayVideo(VideoEntry entry) => _viewModel.LoadVideo(entry);
}
