namespace Mnemora.Contracts;

public sealed record LibrarySectionsPageDto(
    IReadOnlyList<LibrarySectionOverviewDto> Items,
    int NextOffset,
    bool HasMore);