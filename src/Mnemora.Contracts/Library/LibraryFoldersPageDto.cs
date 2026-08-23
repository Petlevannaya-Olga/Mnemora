namespace Mnemora.Contracts.Library;

public sealed record LibraryFoldersPageDto(
    IReadOnlyList<LibraryFolderDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount);
