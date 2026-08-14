using System.Globalization;
using System.Windows.Data;
using Mnemora.Desktop.ViewModels.Topics;

namespace Mnemora.Desktop.Converters;

public sealed class TopicIconConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return TopicAppearanceOptions.GetIconKind(
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