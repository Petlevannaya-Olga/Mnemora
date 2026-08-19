namespace Mnemora.Contracts;

public sealed record LibraryManagementMaterialsPageDto(
    IReadOnlyList<LibraryManagementMaterialOverviewDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount,
    int SourceTotalCount);
