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
        LibraryFolderSnapshotDto? folder = null,
        LibraryMaterialDto? material = null,
        int articleQuestionCount = 0)
    {
        ArgumentNullException.ThrowIfNull(orderItem);

        Id = orderItem.Id;
        Name = orderItem.Name;
        Details = orderItem.Details;
        Target = target;
        Section = section;
        Folder = folder;
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
        Target = LibraryOrderTarget.SECTIONS;
        IconKind = ResolveIcon(section.Icon, Target, Details);
        _position = position;
    }

    /// <summary>
    /// Lightweight first-level folder item for the paged browse mode.
    /// </summary>
    public LibraryManagementOrderItemViewModel(
        LibraryManagementFolderOverviewDto folder,
        int position)
    {
        ArgumentNullException.ThrowIfNull(folder);

        FolderOverview = folder;
        Id = folder.Id;
        Name = folder.Name;
        Details = "Папка";
        Target = LibraryOrderTarget.FOLDERS;
        IconKind = ResolveIcon(folder.Icon, Target, Details);
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
        Target = LibraryOrderTarget.MATERIALS;
        ArticleQuestionCount = material.ArticleQuestionCount;
        IconKind = ResolveIcon(material.Icon, Target, Details);
        _position = position;
    }


    /// <summary>
    /// Плоский элемент управления разделом: папка или материал.
    /// </summary>
    public LibraryManagementOrderItemViewModel(
        LibrarySectionManagementItemDto item,
        int position)
    {
        ArgumentNullException.ThrowIfNull(item);

        SectionManagementItem = item;
        Id = item.Id;
        Name = item.Name;
        Details = item.Kind == LibrarySectionManagementItemKind.FOLDER
            ? "Папка"
            : string.Equals(item.MaterialType, "Question", StringComparison.OrdinalIgnoreCase)
                ? "Вопрос"
                : "Статья";
        Target = LibraryOrderTarget.MATERIALS;
        ArticleQuestionCount = item.ArticleQuestionCount;
        IconKind = ResolveIcon(item.Icon, Target, Details);
        _position = position;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Details { get; }

    public LibraryOrderTarget Target { get; }

    // Full-tree DTOs are retained for explicit order/admin scenarios.
    public LibrarySectionDto? Section { get; }
    public LibraryFolderSnapshotDto? Folder { get; }
    public LibraryMaterialDto? Material { get; }

    // Lightweight DTOs are used for normal paged browsing.
    public LibrarySectionOverviewDto? SectionOverview { get; }
    public LibraryManagementFolderOverviewDto? FolderOverview { get; }
    public LibraryManagementMaterialOverviewDto? MaterialOverview { get; }
    public LibrarySectionManagementItemDto? SectionManagementItem { get; }

    public int ArticleQuestionCount { get; }

    private string? MaterialType => SectionManagementItem?.MaterialType ?? MaterialOverview?.Type ?? Material?.Type;
    private string? MaterialDifficulty => SectionManagementItem?.Difficulty ?? MaterialOverview?.Difficulty ?? Material?.Difficulty;
    private DateTime? MaterialUpdatedAt => SectionManagementItem?.UpdatedAt ?? MaterialOverview?.UpdatedAt ?? Material?.UpdatedAt;
    private DateTime? MaterialCreatedAt => SectionManagementItem?.CreatedAt ?? MaterialOverview?.CreatedAt ?? Material?.CreatedAt;

    public bool IsArticle =>
        string.Equals(MaterialType, "Article", StringComparison.OrdinalIgnoreCase);

    public bool IsLinkedQuestion =>
        MaterialOverview is null &&
        string.Equals(Material?.Type, "Question", StringComparison.OrdinalIgnoreCase) &&
        Material?.ArticleId is not null;

    public bool IsTopLevelMaterial =>
        (SectionManagementItem is not null && !IsManagementFolder) ||
        MaterialOverview is not null ||
        (Material is not null && !IsLinkedQuestion);

    public bool IsManagementFolder =>
        SectionManagementItem?.Kind == LibrarySectionManagementItemKind.FOLDER;

    public string ManagementItemTypeText => IsManagementFolder
        ? "Папка"
        : MaterialTypeText;

    public string ManagementLocationText =>
        SectionManagementItem?.Location ?? string.Empty;

    public string ManagementDifficultyText => IsManagementFolder
        ? "—"
        : DifficultyText;

    public string ManagementQuestionCountText => IsManagementFolder
        ? "—"
        : ArticleQuestionCountText;

    public string ArticleQuestionCountText =>
        IsArticle
            ? FormatCount(ArticleQuestionCount, "вопрос", "вопроса", "вопросов")
            : "—";

    public PackIconKind IconKind { get; }

    public string Color => SectionOverview?.Color ?? Section?.Color ?? string.Empty;

    public string Icon => SectionOverview?.Icon ?? Section?.Icon ?? string.Empty;

    public DateTime CreatedAt => SectionOverview?.CreatedAt ?? Section?.CreatedAt ?? DateTime.MinValue;

    public int FoldersCount => SectionOverview?.FoldersCount ?? Section?.Folders.Count ?? 0;

    public int RootFoldersCount => SectionOverview?.RootFoldersCount ?? Section?.Folders.Count(folder => folder.Depth == 1) ?? 0;

    public int MaterialsCount => SectionOverview?.MaterialsCount ?? GetSectionMaterials().Count(IsTopLevelMaterialDto);

    public string RootFoldersSummaryText => RootFoldersCount == 0
        ? "Папок пока нет"
        : FormatCount(RootFoldersCount, "папка", "папки", "папок");

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
        $"{FormatCount(FoldersCount, "папка", "папки", "папок")} • " +
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

    public string FolderColor => FolderOverview?.Color ?? Folder?.Color ?? string.Empty;

    public DateTime FolderCreatedAt => FolderOverview?.CreatedAt ?? Folder?.CreatedAt ?? DateTime.MinValue;

    public DateTime FolderCreatedAtLocal => FolderOverview is not null
        ? ToLocalTime(FolderOverview.CreatedAt)
        : Folder is null
            ? DateTime.MinValue
            : ToLocalTime(Folder.CreatedAt);

    public int FolderMaterialsCount => FolderOverview?.MaterialsCount ?? (Folder is null ? 0 : GetFolderMaterialsCount(Folder));

    public int FolderArticlesCount => FolderOverview?.ArticlesCount ?? (Folder is null
        ? 0
        : GetFolderMaterials(Folder).Count(material =>
            string.Equals(material.Type, "Article", StringComparison.OrdinalIgnoreCase)));

    public int FolderQuestionsCount => FolderOverview?.QuestionsCount ?? (Folder is null
        ? 0
        : GetFolderMaterials(Folder).Count(material =>
            string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase) &&
            material.ArticleId is null));

    public string FolderMaterialsSummaryText => FolderMaterialsCount == 0
        ? "Материалов пока нет"
        : FormatCount(FolderMaterialsCount, "материал", "материала", "материалов");

    public DateTime FolderLastActivityAt
    {
        get
        {
            if (FolderOverview is not null)
            {
                return FolderOverview.LastActivityAt;
            }

            if (Folder is null)
            {
                return DateTime.MinValue;
            }

            DateTime lastActivityAt = Folder.UpdatedAt > Folder.CreatedAt
                ? Folder.UpdatedAt
                : Folder.CreatedAt;

            foreach (LibraryMaterialDto material in GetFolderMaterials(Folder))
            {
                if (material.UpdatedAt > lastActivityAt)
                {
                    lastActivityAt = material.UpdatedAt;
                }
            }

            return lastActivityAt;
        }
    }

    public DateTime FolderLastActivityAtLocal => FolderLastActivityAt == DateTime.MinValue
        ? DateTime.MinValue
        : ToLocalTime(FolderLastActivityAt);

    public string FolderActivityText => FolderOverview is null && Folder is null
        ? string.Empty
        : FolderLastActivityAt > FolderCreatedAt
            ? $"Активность {FolderLastActivityAtLocal:dd.MM.yyyy}"
            : $"Создана {FolderCreatedAtLocal:dd.MM.yyyy}";

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

        if (FolderOverview is not null)
        {
            return FolderOverview.MaterialsCount;
        }

        if (Section is not null)
        {
            return GetSectionMaterials().Count(IsTopLevelMaterialDto);
        }

        return Folder is not null ? GetFolderMaterialsCount(Folder) : 0;
    }

    private static int GetFolderMaterialsCount(LibraryFolderSnapshotDto folder) =>
        GetFolderMaterials(folder).Count(IsTopLevelMaterialDto);

    private static bool IsTopLevelMaterialDto(LibraryMaterialDto material) =>
        !string.Equals(material.Type, "Question", StringComparison.OrdinalIgnoreCase) ||
        material.ArticleId is null;

    private IReadOnlyList<LibraryMaterialDto> GetSectionMaterials()
    {
        if (Section is null)
        {
            return [];
        }

        return Section.RootMaterials
            .Concat(Section.Folders.SelectMany(GetFolderMaterials))
            .ToArray();
    }

    private static IReadOnlyList<LibraryMaterialDto> GetFolderMaterials(LibraryFolderSnapshotDto folder) =>
        folder.Materials;

    private static DateTime GetLastActivityAt(LibrarySectionDto section) =>
        section.LastActivityAt;

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
            LibraryOrderTarget.SECTIONS => PackIconKind.FolderOutline,
            LibraryOrderTarget.FOLDERS => PackIconKind.BookOpenPageVariant,
            LibraryOrderTarget.MATERIALS when details == "Вопрос" => PackIconKind.HelpCircleOutline,
            _ => PackIconKind.FileDocumentOutline,
        };
    }
}
