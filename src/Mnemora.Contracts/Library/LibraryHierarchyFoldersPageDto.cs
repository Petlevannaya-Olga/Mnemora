namespace Mnemora.Contracts.Library;

public sealed record LibraryHierarchyFoldersPageDto(
    IReadOnlyList<LibraryHierarchyFolderDto> Items,
    int NextOffset,
    bool HasMore);
