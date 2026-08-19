namespace Mnemora.Contracts;

public sealed record LibraryManagementTopicOverviewDto(
    Guid Id,
    Guid SectionId,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int DisplayOrder,
    int MaterialsCount,
    int ArticlesCount,
    int QuestionsCount);
