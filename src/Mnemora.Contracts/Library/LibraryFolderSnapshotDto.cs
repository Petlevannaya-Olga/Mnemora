namespace Mnemora.Contracts.Library;

/// <summary>
/// Folder in the complete library snapshot. Materials contains only direct
/// content; ParentId and Depth describe its position in the hierarchy.
/// </summary>
public sealed record LibraryFolderSnapshotDto(
    Guid Id,
    Guid ParentId,
    int Depth,
    string Name,
    string Color,
    string Icon,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public IReadOnlyList<LibraryMaterialDto> Materials { get; init; } = [];
}
