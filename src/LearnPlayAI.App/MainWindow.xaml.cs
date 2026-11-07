using System.Windows;
using LearnPlayAI.Core.Services;

namespace LearnPlayAI.App;

public partial class MainWindow : Window
{
    public MainWindow(SettingsService settingsService)
    {
        InitializeComponent();
        StatusText.Text = $"LearnPlay AI starting up (theme: {settingsService.Current.Theme})";
    }
}
