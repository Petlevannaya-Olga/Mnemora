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
    string Category,
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
        var definitions = new (
            SectionIcon Value,
            string Name,
            string Category,
            string Kind,
            string Fallback)[]
            {
                (SectionIcon.Folder, "Общее", "Общие", "FolderOutline", "Folder"),
                (SectionIcon.Book, "Книги", "Общие", "BookOpenPageVariantOutline", "BookOutline"),
                (SectionIcon.Brain, "Знания", "Общие", "Brain", "LightbulbOutline"),
                (SectionIcon.Education, "Обучение", "Общие", "SchoolOutline", "BookOutline"),
                (SectionIcon.Document, "Документы", "Общие", "FileDocumentOutline", "FileOutline"),
                (SectionIcon.Team, "Команда", "Общие", "AccountGroupOutline", "AccountMultipleOutline"),
                (SectionIcon.Work, "Работа", "Общие", "BriefcaseOutline", "FolderOutline"),
                (SectionIcon.Finance, "Финансы", "Общие", "Finance", "ChartLine"),
                (SectionIcon.Idea, "Идеи", "Общие", "LightbulbOutline", "Brain"),
                (SectionIcon.Rocket, "Проекты", "Общие", "RocketLaunchOutline", "RocketOutline"),
                (SectionIcon.DotNet, ".NET", "Платформы", "DotNet", "CodeBraces"),
                (SectionIcon.CSharp, "C#", "Платформы", "LanguageCsharp", "CodeBraces"),
                (SectionIcon.AspNet, "ASP.NET", "Платформы", "ApplicationBracesOutline", "Web"),
                (SectionIcon.Blazor, "Blazor", "Платформы", "WebBox", "Web"),
                (SectionIcon.Maui, ".NET MAUI", "Платформы", "CellphoneLink", "Cellphone"),
                (SectionIcon.JavaScript, "JavaScript", "Платформы", "LanguageJavascript", "CodeBraces"),
                (SectionIcon.TypeScript, "TypeScript", "Платформы", "LanguageTypescript", "CodeBraces"),
                (SectionIcon.Python, "Python", "Платформы", "LanguagePython", "CodeBraces"),
                (SectionIcon.Java, "Java", "Платформы", "LanguageJava", "CodeBraces"),
                (SectionIcon.Linux, "Linux", "Платформы", "Linux", "ConsoleLine"),
                (SectionIcon.Windows, "Windows", "Платформы", "MicrosoftWindows", "Monitor"),
                (SectionIcon.Code, "Программирование", "Разработка", "CodeBraces", "CodeTags"),
                (SectionIcon.Api, "API", "Разработка", "Api", "Connection"),
                (SectionIcon.Backend, "Backend", "Разработка", "ServerOutline", "Server"),
                (SectionIcon.Frontend, "Frontend", "Разработка", "MonitorDashboard", "Monitor"),
                (SectionIcon.Web, "Web", "Разработка", "Web", "Monitor"),
                (SectionIcon.Mobile, "Mobile", "Разработка", "Cellphone", "CellphoneLink"),
                (SectionIcon.Desktop, "Desktop", "Разработка", "Monitor", "Laptop"),
                (SectionIcon.Database, "Базы данных", "Данные", "DatabaseOutline", "Database"),
                (SectionIcon.Sql, "SQL", "Данные", "DatabaseSearchOutline", "Database"),
                (SectionIcon.PostgreSql, "PostgreSQL", "Данные", "DatabaseCogOutline", "Database"),
                (SectionIcon.SqlServer, "SQL Server", "Данные", "Microsoft", "Database"),
                (SectionIcon.Redis, "Redis", "Данные", "DatabaseClockOutline", "Database"),
                (SectionIcon.ElasticSearch, "Elasticsearch", "Данные", "Magnify", "DatabaseSearchOutline"),
                (SectionIcon.DataScience, "Data Science", "Данные", "ChartScatterPlot", "ChartLine"),
                (SectionIcon.Architecture, "Архитектура", "Архитектура", "SitemapOutline", "GraphOutline"),
                (SectionIcon.Ddd, "DDD", "Архитектура", "HexagonMultipleOutline", "HexagonOutline"),
                (SectionIcon.Microservices, "Микросервисы", "Архитектура", "HubOutline", "Lan"),
                (SectionIcon.Algorithms, "Алгоритмы", "Архитектура", "FunctionVariant", "CalculatorVariantOutline"),
                (SectionIcon.DataStructures, "Структуры данных", "Архитектура", "GraphOutline", "SitemapOutline"),
                (SectionIcon.Server, "Серверы", "Инфраструктура", "ServerOutline", "Server"),
                (SectionIcon.Cloud, "Облако", "Инфраструктура", "CloudOutline", "Cloud"),
                (SectionIcon.Azure, "Azure", "Инфраструктура", "MicrosoftAzure", "CloudOutline"),
                (SectionIcon.Aws, "AWS", "Инфраструктура", "Aws", "CloudOutline"),
                (SectionIcon.Docker, "Docker", "Инфраструктура", "Docker", "CubeOutline"),
                (SectionIcon.Kubernetes, "Kubernetes", "Инфраструктура", "Kubernetes", "HexagonOutline"),
                (SectionIcon.Git, "Git", "Инфраструктура", "Git", "SourceBranch"),
                (SectionIcon.DevOps, "DevOps", "Инфраструктура", "Infinity", "SourceBranch"),
                (SectionIcon.CiCd, "CI/CD", "Инфраструктура", "SourceBranchSync", "SourceBranch"),
                (SectionIcon.Network, "Сети", "Инфраструктура", "Lan", "AccessPointNetwork"),
                (SectionIcon.Security, "Безопасность", "Качество", "ShieldCheckOutline", "ShieldOutline"),
                (SectionIcon.Testing, "Тестирование", "Качество", "TestTube", "CheckCircleOutline"),
                (SectionIcon.Bug, "Ошибки", "Качество", "BugOutline", "AlertCircleOutline"),
                (SectionIcon.Performance, "Производительность", "Качество", "Speedometer", "ChartLine"),
                (SectionIcon.Monitoring, "Мониторинг", "Качество", "MonitorEye", "Monitor"),
                (SectionIcon.RabbitMq, "RabbitMQ", "Сообщения", "Rabbit", "MessageOutline"),
                (SectionIcon.Kafka, "Kafka", "Сообщения", "MessageTextOutline", "MessageOutline"),
                (SectionIcon.MessageQueue, "Очереди сообщений", "Сообщения", "TrayFull", "MessageOutline"),
                (SectionIcon.ArtificialIntelligence, "Искусственный интеллект", "ИИ", "RobotOutline", "Brain"),
                (SectionIcon.MachineLearning, "Machine Learning", "ИИ", "Brain", "RobotOutline")
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