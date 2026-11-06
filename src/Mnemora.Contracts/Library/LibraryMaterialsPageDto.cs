namespace Mnemora.Contracts.Library;

/// <summary>
/// Страница материалов конкретного контейнера библиотеки.
/// </summary>
public sealed record LibraryMaterialsPageDto(
    LibraryContainerHeaderDto Container,
    IReadOnlyList<LibraryMaterialDto> Items,
    int NextOffset,
    bool HasMore,
    int TotalCount = 0);
