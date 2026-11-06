using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Domain.Sections;
using Mnemora.Domain.LibraryContainers;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed record FolderColorOption(
    FolderColor Value,
    SolidColorBrush Brush);

public sealed record FolderIconOption(
    FolderIcon Value,
    string Name,
    string Category,
    PackIconKind Kind);

public static class TopicAppearanceOptions
{
    public static IReadOnlyList<FolderColorOption> Colors { get; } =
        CreateColors();

    public static IReadOnlyList<FolderIconOption> Icons { get; } =
        CreateIcons();

    private static IReadOnlyList<FolderColorOption> CreateColors()
    {
        var options = new List<FolderColorOption>();

        foreach (var topicColor in Enum.GetValues<FolderColor>())
        {
            if (!Enum.TryParse<SectionColor>(
                    topicColor.ToString(),
                    out var sectionColor))
            {
                throw new InvalidOperationException(
                    $"Для цвета папки '{topicColor}' не найден цвет раздела.");
            }

            var sectionOption = SectionAppearanceOptions.Colors
                .FirstOrDefault(option =>
                    option.Value == sectionColor);

            if (sectionOption is null)
            {
                throw new InvalidOperationException(
                    $"Для цвета папки '{topicColor}' не настроена кисть.");
            }

            options.Add(
                new FolderColorOption(
                    topicColor,
                    sectionOption.Brush));
        }

        return options;
    }

    private static IReadOnlyList<FolderIconOption> CreateIcons()
    {
        var options = new List<FolderIconOption>();

        // Keep previous icon choices and ordering, without using TopicIcon.
        // The original picker started with Bookmark, then the section icons.
        IEnumerable<FolderIcon> iconChoices = new[] { FolderIcon.Bookmark }.Concat(
            Enum.GetValues<SectionIcon>()
                .Where(icon => icon != SectionIcon.Folder)
                .Select(icon => Enum.Parse<FolderIcon>(icon.ToString())));

        foreach (var topicIcon in iconChoices)
        {
            if (topicIcon == FolderIcon.Bookmark)
            {
                options.Add(
                    new FolderIconOption(
                        FolderIcon.Bookmark,
                        "Закладка",
                        "Общие",
                        ResolveIconKind(
                            "BookmarkOutline",
                            "Bookmark")));

                continue;
            }

            if (!Enum.TryParse<SectionIcon>(
                    topicIcon.ToString(),
                    out var sectionIcon))
            {
                throw new InvalidOperationException(
                    $"Для иконки папки '{topicIcon}' не найдена соответствующая иконка раздела.");
            }

            var sectionOption = SectionAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Value == sectionIcon);

            if (sectionOption is null)
            {
                throw new InvalidOperationException(
                    $"Для иконки папки '{topicIcon}' не настроено отображение.");
            }

            options.Add(
                new FolderIconOption(
                    topicIcon,
                    sectionOption.Name,
                    sectionOption.Category,
                    sectionOption.Kind));
        }

        return options;
    }

    public static SolidColorBrush GetColorBrush(
        string? value)
    {
        if (!Enum.TryParse<FolderColor>(
                value,
                true,
                out var color))
        {
            return Colors[0].Brush;
        }

        return Colors
                   .FirstOrDefault(option =>
                       option.Value == color)
                   ?.Brush
               ?? Colors[0].Brush;
    }

    public static PackIconKind GetIconKind(
        string? value)
    {
        if (!Enum.TryParse<FolderIcon>(
                value,
                true,
                out var icon))
        {
            return Icons[0].Kind;
        }

        return Icons
                   .FirstOrDefault(option =>
                       option.Value == icon)
                   ?.Kind
               ?? Icons[0].Kind;
    }

    private static PackIconKind ResolveIconKind(
        string preferredKind,
        string fallbackKind)
    {
        if (Enum.TryParse<PackIconKind>(
                preferredKind,
                out var kind))
        {
            return kind;
        }

        if (Enum.TryParse<PackIconKind>(
                fallbackKind,
                out kind))
        {
            return kind;
        }

        throw new InvalidOperationException(
            $"Не найдена иконка '{preferredKind}' и её замена '{fallbackKind}'.");
    }
}