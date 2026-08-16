using Mnemora.Contracts;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibrarySectionCardViewModel
{
    public LibrarySectionCardViewModel(
        LibrarySectionOverviewDto section,
        int? studiedMaterialsCount = null,
        int plannedMaterialsCount = 0)
    {
        ArgumentNullException.ThrowIfNull(section);

        Source = section;
        StudiedMaterialsCount = studiedMaterialsCount;
        PlannedMaterialsCount = plannedMaterialsCount;
    }

    public LibrarySectionOverviewDto Source { get; }

    public Guid Id => Source.Id;

    public string Name => Source.Name;

    public string Color => Source.Color;

    public string Icon => Source.Icon;

    public DateTime CreatedAt => Source.CreatedAt;

    public DateTime UpdatedAt => Source.UpdatedAt;

    public DateTime LastActivityAt => Source.LastActivityAt;

    public DateTime CreatedAtLocal => ToLocalTime(CreatedAt);

    public DateTime UpdatedAtLocal => ToLocalTime(UpdatedAt);

    public DateTime LastActivityAtLocal => ToLocalTime(LastActivityAt);

    public int TopicsCount => Source.TopicsCount;

    public int MaterialsCount => Source.MaterialsCount;

    public int ArticlesCount => Source.ArticlesCount;

    public int QuestionsCount => Source.QuestionsCount;

    public int? StudiedMaterialsCount { get; }

    public int PlannedMaterialsCount { get; }

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

    public string ActivityText =>
        LastActivityAt > CreatedAt
            ? $"Активность {LastActivityAtLocal:dd.MM.yyyy}"
            : $"Создано {CreatedAtLocal:dd.MM.yyyy}";

    private static DateTime ToLocalTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return utcValue.ToLocalTime();
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
            _ => $"{count} {many}"
        };
    }
}