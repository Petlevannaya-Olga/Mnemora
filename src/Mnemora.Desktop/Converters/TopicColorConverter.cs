using System.Globalization;
using System.Windows.Data;
using Mnemora.Desktop.ViewModels.Topics;

namespace Mnemora.Desktop.Converters;

public sealed class TopicColorConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return TopicAppearanceOptions.GetColorBrush(
            value as string);
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}