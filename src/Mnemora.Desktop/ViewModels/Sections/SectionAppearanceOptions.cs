using System.Windows.Media;
using Mnemora.Shared;
using MaterialDesignThemes.Wpf;
using Mnemora.Domain.Sections;

namespace Mnemora.Desktop.ViewModels.Sections;

public sealed record SectionColorOption(
    SectionColor Value,
    SolidColorBrush Brush);

public sealed record SectionIconOption(
    SectionIcon Value,
    string Name,
    string Category,
    PackIconKind Kind);

public static class SectionAppearanceOptions
{
    public static IReadOnlyList<SectionColorOption> Colors { get; } =
    [
        CreateColor(SectionColor.TEAL, "#16CDB7"),
        CreateColor(SectionColor.CYAN, "#19B9E8"),
        CreateColor(SectionColor.BLUE, "#3978F6"),
        CreateColor(SectionColor.INDIGO, "#536DFE"),
        CreateColor(SectionColor.VIOLET, "#7C4DFF"),
        CreateColor(SectionColor.PURPLE, "#A855F7"),
        CreateColor(SectionColor.PINK, "#E35DB9"),
        CreateColor(SectionColor.CORAL, "#FF6B6B"),
        CreateColor(SectionColor.ORANGE, "#FF9F43"),
        CreateColor(SectionColor.GREEN, "#35B96F"),
    ];

    public static IReadOnlyList<SectionIconOption> Icons { get; } =
        CreateIcons();

    private static IReadOnlyList<SectionIconOption> CreateIcons()
    {
        var definitions = new (
            SectionIcon Value,
            string Name,
            string Category,
            string Kind,
            string Fallback)[]
            {
                (SectionIcon.FOLDER, "Общее", "Общие", "FolderOutline", "Folder"),
                (SectionIcon.BOOK, "Книги", "Общие", "BookOpenPageVariantOutline", "BookOutline"),
                (SectionIcon.BRAIN, "Знания", "Общие", "Brain", "LightbulbOutline"),
                (SectionIcon.EDUCATION, "Обучение", "Общие", "SchoolOutline", "BookOutline"),
                (SectionIcon.DOCUMENT, "Документы", "Общие", "FileDocumentOutline", "FileOutline"),
                (SectionIcon.TEAM, "Команда", "Общие", "AccountGroupOutline", "AccountMultipleOutline"),
                (SectionIcon.WORK, "Работа", "Общие", "BriefcaseOutline", "FolderOutline"),
                (SectionIcon.FINANCE, "Финансы", "Общие", "Finance", "ChartLine"),
                (SectionIcon.IDEA, "Идеи", "Общие", "LightbulbOutline", "Brain"),
                (SectionIcon.ROCKET, "Проекты", "Общие", "RocketLaunchOutline", "RocketOutline"),
                (SectionIcon.DOT_NET, ".NET", "Платформы", "DotNet", "CodeBraces"),
                (SectionIcon.C_SHARP, "C#", "Платформы", "LanguageCsharp", "CodeBraces"),
                (SectionIcon.ASP_NET, "ASP.NET", "Платформы", "ApplicationBracesOutline", "Web"),
                (SectionIcon.BLAZOR, "Blazor", "Платформы", "WebBox", "Web"),
                (SectionIcon.MAUI, ".NET MAUI", "Платформы", "CellphoneLink", "Cellphone"),
                (SectionIcon.JAVA_SCRIPT, "JavaScript", "Платформы", "LanguageJavascript", "CodeBraces"),
                (SectionIcon.TYPE_SCRIPT, "TypeScript", "Платформы", "LanguageTypescript", "CodeBraces"),
                (SectionIcon.PYTHON, "Python", "Платформы", "LanguagePython", "CodeBraces"),
                (SectionIcon.JAVA, "Java", "Платформы", "LanguageJava", "CodeBraces"),
                (SectionIcon.LINUX, "Linux", "Платформы", "Linux", "ConsoleLine"),
                (SectionIcon.WINDOWS, "Windows", "Платформы", "MicrosoftWindows", "Monitor"),
                (SectionIcon.CODE, "Программирование", "Разработка", "CodeBraces", "CodeTags"),
                (SectionIcon.API, "API", "Разработка", "Api", "Connection"),
                (SectionIcon.BACKEND, "Backend", "Разработка", "ServerOutline", "Server"),
                (SectionIcon.FRONTEND, "Frontend", "Разработка", "MonitorDashboard", "Monitor"),
                (SectionIcon.WEB, "Web", "Разработка", "Web", "Monitor"),
                (SectionIcon.MOBILE, "Mobile", "Разработка", "Cellphone", "CellphoneLink"),
                (SectionIcon.DESKTOP, "Desktop", "Разработка", "Monitor", "Laptop"),
                (SectionIcon.DATABASE, "Базы данных", "Данные", "DatabaseOutline", "Database"),
                (SectionIcon.SQL, "SQL", "Данные", "DatabaseSearchOutline", "Database"),
                (SectionIcon.POSTGRE_SQL, "PostgreSQL", "Данные", "DatabaseCogOutline", "Database"),
                (SectionIcon.SQL_SERVER, "SQL Server", "Данные", "Microsoft", "Database"),
                (SectionIcon.REDIS, "Redis", "Данные", "DatabaseClockOutline", "Database"),
                (SectionIcon.ELASTIC_SEARCH, "Elasticsearch", "Данные", "Magnify", "DatabaseSearchOutline"),
                (SectionIcon.DATA_SCIENCE, "Data Science", "Данные", "ChartScatterPlot", "ChartLine"),
                (SectionIcon.ARCHITECTURE, "Архитектура", "Архитектура", "SitemapOutline", "GraphOutline"),
                (SectionIcon.DDD, "DDD", "Архитектура", "HexagonMultipleOutline", "HexagonOutline"),
                (SectionIcon.MICROSERVICES, "Микросервисы", "Архитектура", "HubOutline", "Lan"),
                (SectionIcon.ALGORITHMS, "Алгоритмы", "Архитектура", "FunctionVariant", "CalculatorVariantOutline"),
                (SectionIcon.DATA_STRUCTURES, "Структуры данных", "Архитектура", "GraphOutline", "SitemapOutline"),
                (SectionIcon.SERVER, "Серверы", "Инфраструктура", "ServerOutline", "Server"),
                (SectionIcon.CLOUD, "Облако", "Инфраструктура", "CloudOutline", "Cloud"),
                (SectionIcon.AZURE, "Azure", "Инфраструктура", "MicrosoftAzure", "CloudOutline"),
                (SectionIcon.AWS, "AWS", "Инфраструктура", "Aws", "CloudOutline"),
                (SectionIcon.DOCKER, "Docker", "Инфраструктура", "Docker", "CubeOutline"),
                (SectionIcon.KUBERNETES, "Kubernetes", "Инфраструктура", "Kubernetes", "HexagonOutline"),
                (SectionIcon.GIT, "Git", "Инфраструктура", "Git", "SourceBranch"),
                (SectionIcon.DEV_OPS, "DevOps", "Инфраструктура", "Infinity", "SourceBranch"),
                (SectionIcon.CI_CD, "CI/CD", "Инфраструктура", "SourceBranchSync", "SourceBranch"),
                (SectionIcon.NETWORK, "Сети", "Инфраструктура", "Lan", "AccessPointNetwork"),
                (SectionIcon.SECURITY, "Безопасность", "Качество", "ShieldCheckOutline", "ShieldOutline"),
                (SectionIcon.TESTING, "Тестирование", "Качество", "TestTube", "CheckCircleOutline"),
                (SectionIcon.BUG, "Ошибки", "Качество", "BugOutline", "AlertCircleOutline"),
                (SectionIcon.PERFORMANCE, "Производительность", "Качество", "Speedometer", "ChartLine"),
                (SectionIcon.MONITORING, "Мониторинг", "Качество", "MonitorEye", "Monitor"),
                (SectionIcon.RABBIT_MQ, "RabbitMQ", "Сообщения", "Rabbit", "MessageOutline"),
                (SectionIcon.KAFKA, "Kafka", "Сообщения", "MessageTextOutline", "MessageOutline"),
                (SectionIcon.MESSAGE_QUEUE, "Очереди сообщений", "Сообщения", "TrayFull", "MessageOutline"),
                (SectionIcon.ARTIFICIAL_INTELLIGENCE, "Искусственный интеллект", "ИИ", "RobotOutline", "Brain"),
                (SectionIcon.MACHINE_LEARNING, "Machine Learning", "ИИ", "Brain", "RobotOutline")
            };

        var icons = new List<SectionIconOption>();

        foreach (var definition in definitions)
        {
            var kind = ResolveIconKind(
                definition.Kind,
                definition.Fallback);

            icons.Add(new SectionIconOption(
                definition.Value,
                definition.Name,
                definition.Category,
                kind));
        }

        return icons;
    }

    private static PackIconKind ResolveIconKind(
        string preferredKind,
        string fallbackKind)
    {
        if (Enum.TryParse<PackIconKind>(preferredKind, out var kind))
        {
            return kind;
        }

        if (Enum.TryParse<PackIconKind>(fallbackKind, out kind))
        {
            return kind;
        }

        throw new InvalidOperationException(
            $"Не найдена иконка '{preferredKind}' и её замена '{fallbackKind}'.");
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
        if (!EnumStableNames.TryParseStableName<SectionColor>(value, out var color))
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
        if (!EnumStableNames.TryParseStableName<SectionIcon>(value, out var icon))
        {
            return Icons[0].Kind;
        }

        return Icons
                   .FirstOrDefault(option => option.Value == icon)
                   ?.Kind
               ?? Icons[0].Kind;
    }
}