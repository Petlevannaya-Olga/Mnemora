namespace Mnemora.Contracts;

public sealed record LibraryManagementFoldersPageDto(
    IReadOnlyList<LibraryManagementFolderOverviewDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount);
