namespace Mnemora.Contracts.Library;

public enum LibrarySectionManagementItemKind
{
    Folder,
    Material,
}

public sealed record LibrarySectionManagementItemDto(
    Guid Id,
    LibrarySectionManagementItemKind Kind,
    string Name,
    string Location,
    string Icon,
    string? MaterialType,
    string? Difficulty,
    int ArticleQuestionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int DisplayOrder,
    Guid ContainerId,
    Guid? ParentContainerId);
