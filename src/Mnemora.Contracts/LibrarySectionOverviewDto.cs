namespace Mnemora.Contracts;

public sealed record LibrarySectionOverviewDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int TopicsCount,
    int MaterialsCount,
    int ArticlesCount,
    int QuestionsCount);