namespace Mnemora.Contracts.Library;

public sealed record LibraryTopicsPageDto(
    LibrarySectionHeaderDto Section,
    IReadOnlyList<LibraryTopicOverviewDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount = 0);
