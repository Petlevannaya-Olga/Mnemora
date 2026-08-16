namespace Mnemora.Contracts;

public sealed record LibraryTopicsPageDto(
    LibrarySectionHeaderDto Section,
    IReadOnlyList<LibraryTopicOverviewDto> Items,
    int NextOffset,
    bool HasMore);