namespace Mnemora.Contracts.Library;

public sealed record LibraryManagementSectionsPageDto(
    IReadOnlyList<LibrarySectionOverviewDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount);
