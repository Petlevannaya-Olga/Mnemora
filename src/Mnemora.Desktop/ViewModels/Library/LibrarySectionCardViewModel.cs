using Mnemora.Contracts;
using Mnemora.Domain.Materials;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibrarySectionCardViewModel
{
    public LibrarySectionCardViewModel(
        LibrarySectionDto section,
        int? studiedMaterialsCount = null,
        int plannedMaterialsCount = 0,
        int? sortOrder = null)
    {
        ArgumentNullException.ThrowIfNull(section);

        Source = section;
        StudiedMaterialsCount = studiedMaterialsCount;
        PlannedMaterialsCount = plannedMaterialsCount;
        SortOrder = sortOrder;

        var materials = section.Topics
            .SelectMany(topic => topic.Materials)
            .ToArray();

        MaterialsCount = materials.Length;
        ArticlesCount = materials.Count(material => IsType(material, MaterialType.Article));
        QuestionsCount = materials.Count(material => IsType(material, MaterialType.Question));
    }

    public LibrarySectionDto Source { get; }

    public Guid Id => Source.Id;

    public string Name => Source.Name;

    public string Color => Source.Color;

    public string Icon => Source.Icon;

    public DateTime CreatedAt => Source.CreatedAt;

    public int TopicsCount => Source.Topics.Count;

    public int MaterialsCount { get; }

    public int ArticlesCount { get; }

    public int QuestionsCount { get; }

    public int? StudiedMaterialsCount { get; }

    public int PlannedMaterialsCount { get; }

    public int? SortOrder { get; }

    public bool HasProgress => StudiedMaterialsCount.HasValue && MaterialsCount > 0;

    public bool HasPlannedMaterials => PlannedMaterialsCount > 0;

    public double? ProgressPercentage =>
        HasProgress
            ? StudiedMaterialsCount!.Value * 100d / MaterialsCount
            : null;

    public string StructureText =>
        $"{FormatCount(TopicsCount, "тема", "темы", "тем")} • " +
        FormatCount(MaterialsCount, "материал", "материала", "материалов");

    public string ProgressText =>
        HasProgress
            ? $"{StudiedMaterialsCount} из {MaterialsCount} изучено"
            : string.Empty;

    public string PlannedText => $"{PlannedMaterialsCount} в плане";

    public string MaterialTypesText =>
        $"{FormatCount(ArticlesCount, "статья", "статьи", "статей")} • " +
        FormatCount(QuestionsCount, "вопрос", "вопроса", "вопросов");

    private static bool IsType(LibraryMaterialDto material, MaterialType type)
    {
        return string.Equals(material.Type, type.ToString(), StringComparison.OrdinalIgnoreCase);
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
}