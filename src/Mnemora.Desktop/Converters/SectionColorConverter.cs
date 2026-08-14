using System.Globalization;
using System.Windows.Data;
using Mnemora.Desktop.ViewModels.Sections;

namespace Mnemora.Desktop.Converters;

public sealed class SectionColorConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return SectionAppearanceOptions.GetColorBrush(value as string);
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