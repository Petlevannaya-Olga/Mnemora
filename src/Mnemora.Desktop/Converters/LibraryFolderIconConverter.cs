using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Domain.Sections;

namespace Mnemora.Desktop.Converters;

public sealed class LibraryFolderIconConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        string? icon = value as string;

        if (Enum.TryParse<SectionIcon>(icon, true, out _))
        {
            return SectionAppearanceOptions.GetIconKind(icon);
        }

        return icon?.ToUpperInvariant() switch
        {
            "BOOKMARK" => Resolve("BookmarkOutline", "Bookmark"),
            "QUESTION" => Resolve("HelpCircleOutline", "HelpCircle"),
            "ANSWER" => Resolve("MessageTextOutline", "MessageOutline"),
            _ => Resolve("FolderOutline", "Folder"),
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static PackIconKind Resolve(
        string preferred,
        string fallback)
    {
        if (Enum.TryParse<PackIconKind>(preferred, out var kind))
        {
            return kind;
        }

        return Enum.TryParse<PackIconKind>(fallback, out kind)
            ? kind
            : PackIconKind.Folder;
    }
}
