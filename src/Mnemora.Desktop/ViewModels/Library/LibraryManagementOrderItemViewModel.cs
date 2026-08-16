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
        LibraryMaterialDto? material = null)
    {
        ArgumentNullException.ThrowIfNull(orderItem);

        Id = orderItem.Id;
        Name = orderItem.Name;
        Details = orderItem.Details;
        Target = target;
        Section = section;
        Topic = topic;
        Material = material;
        IconKind = ResolveIcon(orderItem.Icon, target, orderItem.Details);
        _position = position;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Details { get; }

    public LibraryOrderTarget Target { get; }

    public LibrarySectionDto? Section { get; }

    public LibraryTopicDto? Topic { get; }

    public LibraryMaterialDto? Material { get; }

    public PackIconKind IconKind { get; }

    public string Color => Section?.Color ?? string.Empty;

    public string Icon => Section?.Icon ?? string.Empty;

    public DateTime CreatedAt => Section?.CreatedAt ?? DateTime.MinValue;

    public int TopicsCount => Section?.Topics.Count ?? 0;

    public int MaterialsCount => GetSectionMaterials().Count;

    public string TopicsSummaryText => TopicsCount == 0
        ? "Тем пока нет"
        : FormatCount(TopicsCount, "тема", "темы", "тем");

    public string MaterialsSummaryText =>
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    public int ArticlesCount => GetSectionMaterials().Count(material =>
        string.Equals(material.Type, "Article", StringComparison.OrdinalIgnoreCase));

    public int QuestionsCount => GetSectionMaterials().Count(material =>
        string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase));

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

    public string MaterialTypeText => Material?.Type switch
    {
        "Article" => "Статья",
        "Question" => "Вопрос",
        _ => Details,
    };

    public string DifficultyText => Material?.Difficulty switch
    {
        "Easy" => "Легко",
        "Medium" => "Средне",
        "Hard" => "Сложно",
        _ => string.Empty,
    };

    public DateTime? UpdatedAtLocal => Material?.UpdatedAt.ToLocalTime();

    public bool HasItemCount => GetItemCount() > 0;

    public string ItemCountText
    {
        get
        {
            int count = GetItemCount();

            return count > 0
                ? FormatMaterialsCount(count)
                : string.Empty;
        }
    }

    [ObservableProperty]
    private int _position;


    private int GetItemCount()
    {
        if (Section is not null)
        {
            return Section.Topics.Sum(GetTopicMaterialsCount);
        }

        return Topic is not null
            ? GetTopicMaterialsCount(Topic)
            : 0;
    }

    private static int GetTopicMaterialsCount(LibraryTopicDto topic)
    {
        var property = typeof(LibraryTopicDto).GetProperty("Materials");

        return property?.GetValue(topic) is System.Collections.ICollection collection
            ? collection.Count
            : property?.GetValue(topic) is IEnumerable<LibraryMaterialDto> materials
                ? materials.Count()
                : 0;
    }

    private IReadOnlyList<LibraryMaterialDto> GetSectionMaterials()
    {
        if (Section is null)
        {
            return [];
        }

        return Section.Topics
            .SelectMany(GetTopicMaterials)
            .ToArray();
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

        foreach (var topic in section.Topics)
        {
            if (topic.CreatedAt > lastActivityAt)
            {
                lastActivityAt = topic.CreatedAt;
            }

            foreach (var material in GetTopicMaterials(topic))
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
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return utcValue.ToLocalTime();
    }

    private static string FormatMaterialsCount(int count)
    {
        return FormatCount(count, "материал", "материала", "материалов");
    }

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
