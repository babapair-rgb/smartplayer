using System.Globalization;
using System.Windows.Data;

namespace LearnPlayAI.App.Converters;

public sealed class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var seconds = value is double d ? d : 0d;
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
