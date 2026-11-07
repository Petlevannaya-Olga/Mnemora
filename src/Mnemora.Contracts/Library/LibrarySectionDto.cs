namespace Mnemora.Contracts.Library;

/// <summary>
/// Complete library snapshot for management actions. Folders is a flat list
/// of all nested folders; RootMaterials holds content directly under the section.
/// </summary>
public sealed record LibrarySectionDto(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    IReadOnlyList<LibraryFolderSnapshotDto> Folders,
    IReadOnlyList<LibraryMaterialDto> RootMaterials);
