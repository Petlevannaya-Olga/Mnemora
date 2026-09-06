namespace Mnemora.Contracts.Library;

public sealed record LibraryMaterialDto(
    Guid Id,
    Guid TopicId,
    string Title,
    string Type,
    string Difficulty,
    string Icon,
    int StudyPoints,
    int ReviewPoints,
    int LearningRevision,
    IReadOnlyList<string> Tags,
    Guid? ArticleId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Фактическое расположение материала в новой структуре библиотеки.
    /// TopicId пока сохраняется как legacy-связь до удаления Topic.
    /// </summary>
    public Guid ContainerId { get; init; } = TopicId;
}
