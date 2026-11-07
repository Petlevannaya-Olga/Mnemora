using System.Text.Json.Serialization;
namespace Mnemora.Contracts.Library;

public enum LibrarySectionManagementItemKind
{
    [JsonStringEnumMemberName("Folder")]
    FOLDER,
    [JsonStringEnumMemberName("Material")]
    MATERIAL,
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
