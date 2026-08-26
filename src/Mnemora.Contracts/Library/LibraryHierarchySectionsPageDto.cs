namespace Mnemora.Contracts.Library;

public sealed record LibraryHierarchySectionsPageDto(
    IReadOnlyList<LibraryHierarchySectionDto> Items,
    int NextOffset,
    bool HasMore);
