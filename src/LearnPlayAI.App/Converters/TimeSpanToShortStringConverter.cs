using System.Globalization;
using System.Windows.Data;

namespace LearnPlayAI.App.Converters;

public sealed class TimeSpanToShortStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var span = value is TimeSpan ts ? ts : TimeSpan.Zero;
        return span.Hours > 0 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
