namespace Mnemora.Contracts;

public sealed record LibraryManagementTopicsPageDto(
    IReadOnlyList<LibraryManagementTopicOverviewDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount);
