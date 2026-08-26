namespace Mnemora.Contracts.Library;

public sealed record LibraryHierarchyFolderDto(
    Guid Id,
    Guid SectionId,
    Guid ParentId,
    string Name,
    string Color,
    string Icon,
    int ChildFoldersCount);
