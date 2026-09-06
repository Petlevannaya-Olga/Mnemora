namespace Mnemora.Contracts.Library;

public sealed record LibrarySectionManagementItemsPageDto(
    IReadOnlyList<LibrarySectionManagementItemDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount,
    int SourceTotalCount);
