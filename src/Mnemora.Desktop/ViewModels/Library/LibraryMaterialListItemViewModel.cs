using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed class LibraryMaterialListItemViewModel
{
    public LibraryMaterialListItemViewModel(LibraryMaterialDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    public LibraryMaterialDto Source { get; }

    public Guid Id => Source.Id;

    public string Title => Source.Title;

    public string Icon => Source.Icon;

    public int StudyPoints => Source.StudyPoints;

    public int ReviewPoints => Source.ReviewPoints;

    public int LearningRevision => Source.LearningRevision;

    public IReadOnlyList<string> Tags => Source.Tags;

    public DateTime CreatedAtLocal => ToLocalTime(Source.CreatedAt);

    public DateTime UpdatedAtLocal => ToLocalTime(Source.UpdatedAt);

    public bool IsArticle => string.Equals(
        Source.Type,
        "Article",
        StringComparison.OrdinalIgnoreCase);

    public bool IsQuestion => string.Equals(
        Source.Type,
        "Question",
        StringComparison.OrdinalIgnoreCase);

    public string TypeTitle => Source.Type switch
    {
        "Article" => "Статья",
        "Question" => "Вопрос",
        _ => Source.Type
    };

    public string DifficultyTitle => Source.Difficulty switch
    {
        "Easy" => "Простая",
        "Medium" => "Средняя",
        "Hard" => "Сложная",
        _ => Source.Difficulty
    };

    public string TagsText => Tags.Count == 0
        ? "—"
        : string.Join(", ", Tags);

    public string PointsText =>
        $"{StudyPoints} за изучение • {ReviewPoints} за повторение";
    
    public bool IsEasy => string.Equals(
        Source.Difficulty,
        "Easy",
        StringComparison.OrdinalIgnoreCase);

    public bool IsMedium => string.Equals(
        Source.Difficulty,
        "Medium",
        StringComparison.OrdinalIgnoreCase);

    public bool IsHard => string.Equals(
        Source.Difficulty,
        "Hard",
        StringComparison.OrdinalIgnoreCase);

    private static DateTime ToLocalTime(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return utcValue.ToLocalTime();
    }
}