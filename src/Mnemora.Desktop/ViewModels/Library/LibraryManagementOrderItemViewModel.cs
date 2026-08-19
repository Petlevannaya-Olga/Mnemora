using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using Mnemora.Application.Library.Order;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryManagementOrderItemViewModel : ObservableObject
{
    public LibraryManagementOrderItemViewModel(
        LibraryOrderItemDto orderItem,
        LibraryOrderTarget target,
        int position,
        LibrarySectionDto? section = null,
        LibraryTopicDto? topic = null,
        LibraryMaterialDto? material = null,
        int articleQuestionCount = 0)
    {
        ArgumentNullException.ThrowIfNull(orderItem);

        Id = orderItem.Id;
        Name = orderItem.Name;
        Details = orderItem.Details;
        Target = target;
        Section = section;
        Topic = topic;
        Material = material;
        ArticleQuestionCount = articleQuestionCount;
        IconKind = ResolveIcon(orderItem.Icon, target, orderItem.Details);
        _position = position;
    }

    /// <summary>
    /// Lightweight section item for the paged browse mode. It deliberately does not
    /// carry the complete LibrarySectionDto tree.
    /// </summary>
    public LibraryManagementOrderItemViewModel(
        LibrarySectionOverviewDto section,
        int position)
    {
        ArgumentNullException.ThrowIfNull(section);

        SectionOverview = section;
        Id = section.Id;
        Name = section.Name;
        Details = "Раздел";
        Target = LibraryOrderTarget.Sections;
        IconKind = ResolveIcon(section.Icon, Target, Details);
        _position = position;
    }

    /// <summary>
    /// Lightweight topic item for the paged browse mode.
    /// </summary>
    public LibraryManagementOrderItemViewModel(
        LibraryManagementTopicOverviewDto topic,
        int position)
    {
        ArgumentNullException.ThrowIfNull(topic);

        TopicOverview = topic;
        Id = topic.Id;
        Name = topic.Name;
        Details = "Тема";
        Target = LibraryOrderTarget.Topics;
        IconKind = ResolveIcon(topic.Icon, Target, Details);
        _position = position;
    }

    /// <summary>
    /// Lightweight top-level material item for the paged browse mode.
    /// Linked questions never reach this constructor because they are filtered in SQL.
    /// </summary>
    public LibraryManagementOrderItemViewModel(
        LibraryManagementMaterialOverviewDto material,
        int position)
    {
        ArgumentNullException.ThrowIfNull(material);

        MaterialOverview = material;
        Id = material.Id;
        Name = material.Title;
        Details = string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase)
            ? "Вопрос"
            : "Статья";
        Target = LibraryOrderTarget.Materials;
        ArticleQuestionCount = material.ArticleQuestionCount;
        IconKind = ResolveIcon(material.Icon, Target, Details);
        _position = position;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Details { get; }

    public LibraryOrderTarget Target { get; }

    // Full-tree DTOs are retained for explicit order/admin scenarios.
    public LibrarySectionDto? Section { get; }
    public LibraryTopicDto? Topic { get; }
    public LibraryMaterialDto? Material { get; }

    // Lightweight DTOs are used for normal paged browsing.
    public LibrarySectionOverviewDto? SectionOverview { get; }
    public LibraryManagementTopicOverviewDto? TopicOverview { get; }
    public LibraryManagementMaterialOverviewDto? MaterialOverview { get; }

    public int ArticleQuestionCount { get; }

    private string? MaterialType => MaterialOverview?.Type ?? Material?.Type;
    private string? MaterialDifficulty => MaterialOverview?.Difficulty ?? Material?.Difficulty;
    private DateTime? MaterialUpdatedAt => MaterialOverview?.UpdatedAt ?? Material?.UpdatedAt;
    private DateTime? MaterialCreatedAt => MaterialOverview?.CreatedAt ?? Material?.CreatedAt;

    public bool IsArticle =>
        string.Equals(MaterialType, "Article", StringComparison.OrdinalIgnoreCase);

    public bool IsLinkedQuestion =>
        MaterialOverview is null &&
        string.Equals(Material?.Type, "Question", StringComparison.OrdinalIgnoreCase) &&
        Material?.ArticleId is not null;

    public bool IsTopLevelMaterial =>
        MaterialOverview is not null ||
        (Material is not null && !IsLinkedQuestion);

    public string ArticleQuestionCountText =>
        IsArticle
            ? FormatCount(ArticleQuestionCount, "вопрос", "вопроса", "вопросов")
            : "—";

    public PackIconKind IconKind { get; }

    public string Color => SectionOverview?.Color ?? Section?.Color ?? string.Empty;

    public string Icon => SectionOverview?.Icon ?? Section?.Icon ?? string.Empty;

    public DateTime CreatedAt => SectionOverview?.CreatedAt ?? Section?.CreatedAt ?? DateTime.MinValue;

    public int TopicsCount => SectionOverview?.TopicsCount ?? Section?.Topics.Count ?? 0;

    public int MaterialsCount => SectionOverview?.MaterialsCount ?? GetSectionMaterials().Count(IsTopLevelMaterialDto);

    public string TopicsSummaryText => TopicsCount == 0
        ? "Тем пока нет"
        : FormatCount(TopicsCount, "тема", "темы", "тем");

    public string MaterialsSummaryText =>
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    public int ArticlesCount => SectionOverview?.ArticlesCount ?? GetSectionMaterials().Count(material =>
        string.Equals(material.Type, "Article", StringComparison.OrdinalIgnoreCase));

    public int QuestionsCount => SectionOverview?.QuestionsCount ?? GetSectionMaterials().Count(material =>
        string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase) &&
        material.ArticleId is null);

    public bool HasProgress => false;

    public string ProgressText => string.Empty;

    public double? ProgressPercentage => null;

    public string StructureText =>
        $"{FormatCount(TopicsCount, "тема", "темы", "тем")} • " +
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    public string MaterialTypesText =>
        $"{FormatCount(ArticlesCount, "статья", "статьи", "статей")} • " +
        FormatCount(QuestionsCount, "вопрос", "вопроса", "вопросов");

    public string ActivityText
    {
        get
        {
            if (SectionOverview is not null)
            {
                return SectionOverview.LastActivityAt > SectionOverview.CreatedAt
                    ? $"Активность {ToLocalTime(SectionOverview.LastActivityAt):dd.MM.yyyy}"
                    : $"Создано {ToLocalTime(SectionOverview.CreatedAt):dd.MM.yyyy}";
            }

            if (Section is null)
            {
                return string.Empty;
            }

            DateTime lastActivityAt = GetLastActivityAt(Section);

            return lastActivityAt > Section.CreatedAt
                ? $"Активность {ToLocalTime(lastActivityAt):dd.MM.yyyy}"
                : $"Создано {ToLocalTime(Section.CreatedAt):dd.MM.yyyy}";
        }
    }

    public string MaterialTypeText => MaterialType switch
    {
        "Article" => "Статья",
        "Question" => "Вопрос",
        _ => Details,
    };

    public string DifficultyText => MaterialDifficulty switch
    {
        "Easy" => "Легко",
        "Medium" => "Средне",
        "Hard" => "Сложно",
        _ => string.Empty,
    };

    public DateTime? UpdatedAtLocal => MaterialUpdatedAt?.ToLocalTime();

    public string TopicColor => TopicOverview?.Color ?? Topic?.Color ?? string.Empty;

    public DateTime TopicCreatedAt => TopicOverview?.CreatedAt ?? Topic?.CreatedAt ?? DateTime.MinValue;

    public DateTime TopicCreatedAtLocal => TopicOverview is not null
        ? ToLocalTime(TopicOverview.CreatedAt)
        : Topic is null
            ? DateTime.MinValue
            : ToLocalTime(Topic.CreatedAt);

    public int TopicMaterialsCount => TopicOverview?.MaterialsCount ?? (Topic is null ? 0 : GetTopicMaterialsCount(Topic));

    public int TopicArticlesCount => TopicOverview?.ArticlesCount ?? (Topic is null
        ? 0
        : GetTopicMaterials(Topic).Count(material =>
            string.Equals(material.Type, "Article", StringComparison.OrdinalIgnoreCase)));

    public int TopicQuestionsCount => TopicOverview?.QuestionsCount ?? (Topic is null
        ? 0
        : GetTopicMaterials(Topic).Count(material =>
            string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase) &&
            material.ArticleId is null));

    public string TopicMaterialsSummaryText => TopicMaterialsCount == 0
        ? "Материалов пока нет"
        : FormatCount(TopicMaterialsCount, "материал", "материала", "материалов");

    public DateTime TopicLastActivityAt
    {
        get
        {
            if (TopicOverview is not null)
            {
                return TopicOverview.LastActivityAt;
            }

            if (Topic is null)
            {
                return DateTime.MinValue;
            }

            DateTime lastActivityAt = Topic.UpdatedAt > Topic.CreatedAt
                ? Topic.UpdatedAt
                : Topic.CreatedAt;

            foreach (LibraryMaterialDto material in GetTopicMaterials(Topic))
            {
                if (material.UpdatedAt > lastActivityAt)
                {
                    lastActivityAt = material.UpdatedAt;
                }
            }

            return lastActivityAt;
        }
    }

    public DateTime TopicLastActivityAtLocal => TopicLastActivityAt == DateTime.MinValue
        ? DateTime.MinValue
        : ToLocalTime(TopicLastActivityAt);

    public string TopicActivityText => TopicOverview is null && Topic is null
        ? string.Empty
        : TopicLastActivityAt > TopicCreatedAt
            ? $"Активность {TopicLastActivityAtLocal:dd.MM.yyyy}"
            : $"Создана {TopicCreatedAtLocal:dd.MM.yyyy}";

    public bool HasItemCount => GetItemCount() > 0;

    public string ItemCountText
    {
        get
        {
            int count = GetItemCount();
            return count > 0 ? FormatMaterialsCount(count) : string.Empty;
        }
    }

    [ObservableProperty]
    private int _position;

    private int GetItemCount()
    {
        if (SectionOverview is not null)
        {
            return SectionOverview.MaterialsCount;
        }

        if (TopicOverview is not null)
        {
            return TopicOverview.MaterialsCount;
        }

        if (Section is not null)
        {
            return Section.Topics.Sum(GetTopicMaterialsCount);
        }

        return Topic is not null ? GetTopicMaterialsCount(Topic) : 0;
    }

    private static int GetTopicMaterialsCount(LibraryTopicDto topic) =>
        GetTopicMaterials(topic).Count(IsTopLevelMaterialDto);

    private static bool IsTopLevelMaterialDto(LibraryMaterialDto material) =>
        !string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase) ||
        material.ArticleId is null;

    private IReadOnlyList<LibraryMaterialDto> GetSectionMaterials()
    {
        if (Section is null)
        {
            return [];
        }

        return Section.Topics.SelectMany(GetTopicMaterials).ToArray();
    }

    private static IReadOnlyList<LibraryMaterialDto> GetTopicMaterials(LibraryTopicDto topic)
    {
        var property = typeof(LibraryTopicDto).GetProperty("Materials");

        return property?.GetValue(topic) is IEnumerable<LibraryMaterialDto> materials
            ? materials.ToArray()
            : [];
    }

    private static DateTime GetLastActivityAt(LibrarySectionDto section)
    {
        DateTime lastActivityAt = section.CreatedAt;

        foreach (LibraryTopicDto topic in section.Topics)
        {
            if (topic.CreatedAt > lastActivityAt)
            {
                lastActivityAt = topic.CreatedAt;
            }

            foreach (LibraryMaterialDto material in GetTopicMaterials(topic))
            {
                if (material.UpdatedAt > lastActivityAt)
                {
                    lastActivityAt = material.UpdatedAt;
                }
            }
        }

        return lastActivityAt;
    }

    private static DateTime ToLocalTime(DateTime value)
    {
        DateTime utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return utcValue.ToLocalTime();
    }

    private static string FormatMaterialsCount(int count) =>
        FormatCount(count, "материал", "материала", "материалов");

    private static string FormatCount(int count, string one, string few, string many)
    {
        int lastTwoDigits = count % 100;

        if (lastTwoDigits is >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return (count % 10) switch
        {
            1 => $"{count} {one}",
            2 or 3 or 4 => $"{count} {few}",
            _ => $"{count} {many}",
        };
    }

    private static PackIconKind ResolveIcon(
        string icon,
        LibraryOrderTarget target,
        string details)
    {
        if (Enum.TryParse(icon, true, out PackIconKind parsedIcon))
        {
            return parsedIcon;
        }

        return target switch
        {
            LibraryOrderTarget.Sections => PackIconKind.FolderOutline,
            LibraryOrderTarget.Topics => PackIconKind.BookOpenPageVariant,
            LibraryOrderTarget.Materials when details == "Вопрос" => PackIconKind.HelpCircleOutline,
            _ => PackIconKind.FileDocumentOutline,
        };
    }
}
