using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryTopicCardViewModel
{
    public LibraryTopicCardViewModel(LibraryTopicOverviewDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    public LibraryTopicOverviewDto Source { get; }

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

    public int MaterialsCount => Source.MaterialsCount;

    public int ArticlesCount => Source.ArticlesCount;

    public int QuestionsCount => Source.QuestionsCount;

    public string MaterialsText => FormatCount(
        MaterialsCount,
        "материал",
        "материала",
        "материалов");

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