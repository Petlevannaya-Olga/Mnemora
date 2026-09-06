namespace Mnemora.Contracts.Library;

public sealed record LibrarySectionOverviewDto(
    Guid Id,
    Guid RootContainerId,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int FoldersCount,
    int TopicsCount,
    int MaterialsCount,
    int ArticlesCount,
    int QuestionsCount);
