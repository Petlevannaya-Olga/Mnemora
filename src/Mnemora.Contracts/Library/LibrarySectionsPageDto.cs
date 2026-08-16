namespace Mnemora.Contracts.Library;

public sealed record LibrarySectionsPageDto(
    IReadOnlyList<LibrarySectionOverviewDto> Items,
    int NextOffset,
    bool HasMore);