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
    DateTime UpdatedAt);