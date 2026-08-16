namespace Mnemora.Contracts;

public sealed record LibraryTopicOverviewDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int MaterialsCount,
    int ArticlesCount,
    int QuestionsCount);