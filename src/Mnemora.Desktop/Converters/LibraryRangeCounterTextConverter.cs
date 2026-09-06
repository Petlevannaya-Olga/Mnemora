using System.Globalization;
using System.Windows.Data;

namespace Mnemora.Desktop.Converters;

public sealed class LibraryRangeCounterTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return value ?? string.Empty;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                return text[i..];
            }
        }

        return text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
