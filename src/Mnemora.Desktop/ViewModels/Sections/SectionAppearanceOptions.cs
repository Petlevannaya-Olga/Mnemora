using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Mnemora.Domain.Sections;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed record SectionColorOption(
    SectionColor Value,
    SolidColorBrush Brush);

public sealed record SectionIconOption(
    SectionIcon Value,
    string Name,
    PackIconKind Kind);

public static class SectionAppearanceOptions
{
    public static IReadOnlyList<SectionColorOption> Colors { get; } =
    [
        CreateColor(SectionColor.Teal, "#16CDB7"),
        CreateColor(SectionColor.Cyan, "#19B9E8"),
        CreateColor(SectionColor.Blue, "#3978F6"),
        CreateColor(SectionColor.Indigo, "#536DFE"),
        CreateColor(SectionColor.Violet, "#7C4DFF"),
        CreateColor(SectionColor.Purple, "#A855F7"),
        CreateColor(SectionColor.Pink, "#E35DB9"),
        CreateColor(SectionColor.Coral, "#FF6B6B"),
        CreateColor(SectionColor.Orange, "#FF9F43"),
        CreateColor(SectionColor.Green, "#35B96F"),
    ];

    public static IReadOnlyList<SectionIconOption> Icons { get; } =
        CreateIcons();

    private static IReadOnlyList<SectionIconOption> CreateIcons()
    {
        var definitions = new (SectionIcon Value, string Name, string Kind)[]
        {
            (SectionIcon.Folder, "Папка", "FolderOutline"), (SectionIcon.Code, "Код", "CodeBraces"),
            (SectionIcon.Database, "База данных", "DatabaseOutline"),
            (SectionIcon.Server, "Сервер", "ServerOutline"), (SectionIcon.Cloud, "Облако", "CloudOutline"),
            (SectionIcon.Book, "Книга", "BookOpenPageVariantOutline"), (SectionIcon.Brain, "Знания", "Brain"),
            (SectionIcon.Education, "Обучение", "SchoolOutline"), (SectionIcon.Web, "Веб", "Web"),
            (SectionIcon.Api, "API", "Api"), (SectionIcon.Console, "Консоль", "ConsoleLine"),
            (SectionIcon.CSharp, "C#", "LanguageCsharp"), (SectionIcon.Git, "Git", "Git"),
            (SectionIcon.Docker, "Docker", "Docker"), (SectionIcon.Kubernetes, "Kubernetes", "Kubernetes"),
            (SectionIcon.Azure, "Azure", "MicrosoftAzure"),
            (SectionIcon.Security, "Безопасность", "ShieldCheckOutline"),
            (SectionIcon.Testing, "Тестирование", "TestTube"), (SectionIcon.Bug, "Ошибки", "BugOutline"),
            (SectionIcon.Settings, "Настройки", "CogOutline"), (SectionIcon.Team, "Команда", "AccountGroupOutline"),
            (SectionIcon.Work, "Работа", "BriefcaseOutline"), (SectionIcon.Finance, "Финансы", "Finance"),
            (SectionIcon.Idea, "Идеи", "LightbulbOutline"), (SectionIcon.Rocket, "Проекты", "RocketLaunchOutline"),
            (SectionIcon.Mobile, "Мобильная разработка", "Cellphone"), (SectionIcon.Desktop, "Desktop", "Monitor"),
            (SectionIcon.Network, "Сети", "Lan"), (SectionIcon.Document, "Документы", "FileDocumentOutline"),
            (SectionIcon.Chart, "Аналитика", "ChartLine"),
            (SectionIcon.Calculator, "Алгоритмы", "CalculatorVariantOutline"),
            (SectionIcon.Architecture, "Архитектура", "SitemapOutline"),
        };

        var icons = new List<SectionIconOption>();

        foreach (var definition in definitions)
        {
            if (!Enum.TryParse<PackIconKind>(definition.Kind, out var kind))
            {
                continue;
            }

            icons.Add(new SectionIconOption(
                definition.Value,
                definition.Name,
                kind));
        }

        if (icons.Count == 0)
        {
            throw new InvalidOperationException(
                "Не удалось найти доступные иконки Material Design.");
        }

        return icons;
    }

    private static SectionColorOption CreateColor(
        SectionColor value,
        string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        return new SectionColorOption(value, brush);
    }

    public static SolidColorBrush GetColorBrush(string? value)
    {
        if (!Enum.TryParse<SectionColor>(value, true, out var color))
        {
            return Colors[0].Brush;
        }

        return Colors
                   .FirstOrDefault(option => option.Value == color)
                   ?.Brush
               ?? Colors[0].Brush;
    }

    public static PackIconKind GetIconKind(string? value)
    {
        if (!Enum.TryParse<SectionIcon>(value, true, out var icon))
        {
            return Icons[0].Kind;
        }

        return Icons
                   .FirstOrDefault(option => option.Value == icon)
                   ?.Kind
               ?? Icons[0].Kind;
    }
}