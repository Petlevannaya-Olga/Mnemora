namespace Mnemora.Contracts.Library;

public sealed record LibraryMaterialsPageDto(
    LibraryTopicHeaderDto Topic,
    IReadOnlyList<LibraryMaterialDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount = 0);
