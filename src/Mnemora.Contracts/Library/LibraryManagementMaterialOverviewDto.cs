namespace Mnemora.Contracts;

public sealed record LibraryManagementMaterialOverviewDto(
    Guid Id,
    Guid TopicId,
    string Title,
    string Type,
    string Difficulty,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int DisplayOrder,
    int ArticleQuestionCount)
{
    /// <summary>
    /// Фактическое расположение материала. TopicId остаётся legacy-полем.
    /// </summary>
    public Guid ContainerId { get; init; } = TopicId;
}
