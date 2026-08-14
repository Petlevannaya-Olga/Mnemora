using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;

namespace Mnemora.Desktop.ViewModels.Topics;

public sealed record TopicColorOption(
    TopicColor Value,
    SolidColorBrush Brush);

public sealed record TopicIconOption(
    TopicIcon Value,
    string Name,
    string Category,
    PackIconKind Kind);

public static class TopicAppearanceOptions
{
    public static IReadOnlyList<TopicColorOption> Colors { get; } =
        CreateColors();

    public static IReadOnlyList<TopicIconOption> Icons { get; } =
        CreateIcons();

    private static IReadOnlyList<TopicColorOption> CreateColors()
    {
        var options = new List<TopicColorOption>();

        foreach (var topicColor in Enum.GetValues<TopicColor>())
        {
            if (!Enum.TryParse<SectionColor>(
                    topicColor.ToString(),
                    out var sectionColor))
            {
                throw new InvalidOperationException(
                    $"Для цвета темы '{topicColor}' не найден цвет раздела.");
            }

            var sectionOption = SectionAppearanceOptions.Colors
                .FirstOrDefault(option =>
                    option.Value == sectionColor);

            if (sectionOption is null)
            {
                throw new InvalidOperationException(
                    $"Для цвета темы '{topicColor}' не настроена кисть.");
            }

            options.Add(
                new TopicColorOption(
                    topicColor,
                    sectionOption.Brush));
        }

        return options;
    }

    private static IReadOnlyList<TopicIconOption> CreateIcons()
    {
        var options = new List<TopicIconOption>();

        foreach (var topicIcon in Enum.GetValues<TopicIcon>())
        {
            if (topicIcon == TopicIcon.Bookmark)
            {
                options.Add(
                    new TopicIconOption(
                        TopicIcon.Bookmark,
                        "Тема",
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
                    $"Для иконки темы '{topicIcon}' не найдена соответствующая иконка раздела.");
            }

            var sectionOption = SectionAppearanceOptions.Icons
                .FirstOrDefault(option =>
                    option.Value == sectionIcon);

            if (sectionOption is null)
            {
                throw new InvalidOperationException(
                    $"Для иконки темы '{topicIcon}' не настроено отображение.");
            }

            options.Add(
                new TopicIconOption(
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
        if (!Enum.TryParse<TopicColor>(
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
        if (!Enum.TryParse<TopicIcon>(
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