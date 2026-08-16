namespace Mnemora.Contracts;

public sealed record LibraryMaterialsPageDto(
    LibraryTopicHeaderDto Topic,
    IReadOnlyList<LibraryMaterialDto> Items,
    int NextOffset,
    bool HasMore);