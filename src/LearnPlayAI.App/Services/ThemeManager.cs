using System.Linq;
using System.Windows;
using LearnPlayAI.Core.Models;

namespace LearnPlayAI.App.Services;

public sealed class ThemeManager
{
    public void Apply(ThemeMode theme)
    {
        var dictUri = theme == ThemeMode.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var newDictionary = new ResourceDictionary { Source = dictUri };
        var app = Application.Current;

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Themes/"));
        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }
        app.Resources.MergedDictionaries.Add(newDictionary);
    }
}
